using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Infrastructure.Data.Auditing;

public sealed class ApplicationAuditReconciler(
    ApplicationDbContext dbContext,
    IApplicationMutationManifestVerifier manifestVerifier,
    IOptions<ApplicationAuditReconciliationOptions> options,
    ApplicationAuditReconciliationMetrics metrics,
    TimeProvider timeProvider)
    : IApplicationAuditReconciler
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly IApplicationMutationManifestVerifier _manifestVerifier =
        manifestVerifier ?? throw new ArgumentNullException(nameof(manifestVerifier));
    private readonly ApplicationAuditReconciliationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ApplicationAuditReconciliationMetrics _metrics =
        metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<ApplicationAuditReconciliationSummary> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            return ApplicationAuditReconciliationMetrics.DisabledSummary;
        }

        DateTime now = UtcNow();
        List<string> batchIds = await _dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.MutationBatchId != string.Empty)
            .GroupBy(record => record.MutationBatchId)
            .OrderByDescending(group => group.Max(record => record.ModifiedOnUtc))
            .Select(group => group.Key)
            .Take(_options.MaximumBatchesPerRun)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<AuditRecord> auditRecords = await _dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => batchIds.Contains(record.MutationBatchId) || record.MutationBatchId == string.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ApplicationAuditCompletionOutboxEntry> completionEntries = await _dbContext
            .ApplicationAuditCompletionOutboxEntries
            .AsNoTracking()
            .Where(entry => batchIds.Contains(entry.MutationBatchId) ||
                entry.Status != ApplicationAuditCompletionOutboxStatuses.Delivered)
            .OrderByDescending(entry => entry.CreatedUtc)
            .Take(_options.MaximumBatchesPerRun * 2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ApplicationAuditReconciliationCandidate> candidates = BuildCandidates(
            auditRecords,
            completionEntries,
            now);

        await PersistCandidatesAsync(candidates, batchIds, completionEntries, now, cancellationToken)
            .ConfigureAwait(false);

        ApplicationAuditReconciliationSummary summary = await GetSummaryCoreAsync(now, cancellationToken)
            .ConfigureAwait(false);
        _metrics.Update(summary);
        return summary;
    }

    public async Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            return ApplicationAuditReconciliationMetrics.DisabledSummary;
        }

        ApplicationAuditReconciliationSummary summary = await GetSummaryCoreAsync(
                _metrics.LastRunUtc,
                cancellationToken)
            .ConfigureAwait(false);
        _metrics.Update(summary);
        return summary;
    }

    public async Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
        ApplicationAuditReconciliationQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IQueryable<ApplicationAuditReconciliationFinding> query = _dbContext
            .ApplicationAuditReconciliationFindings
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            string reasonCode = request.ReasonCode.Trim();
            query = query.Where(finding => finding.ReasonCode == reasonCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            string severity = request.Severity.Trim();
            query = query.Where(finding => finding.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(request.MutationBatchId))
        {
            string batchId = request.MutationBatchId.Trim();
            query = query.Where(finding => finding.MutationBatchId == batchId);
        }

        if (!string.IsNullOrWhiteSpace(request.RemediationStatus))
        {
            string status = request.RemediationStatus.Trim();
            query = query.Where(finding => finding.RemediationStatus == status);
        }

        int maximumResults = Math.Clamp(request.MaximumResults, 1, 500);
        return await query
            .OrderByDescending(finding => finding.LastObservedUtc)
            .Take(maximumResults)
            .Select(finding => new ApplicationAuditReconciliationFindingItem(
                finding.Id,
                finding.SchemaVersion,
                finding.FindingKey,
                finding.ReasonCode,
                finding.Severity,
                finding.MutationBatchId,
                finding.Destination,
                finding.Guidance,
                finding.RemediationStatus,
                finding.FirstObservedUtc,
                finding.LastObservedUtc,
                finding.ResolvedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
        Guid findingId,
        ApplicationAuditReconciliationRemediationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string actionCode = NormalizeRequired(request.ActionCode, 64, nameof(request.ActionCode));
        string actorId = NormalizeRequired(request.ActorId, 256, nameof(request.ActorId));
        string? evidenceReference = NormalizeOptional(request.EvidenceReference, 256);

        // When the caller already owns an EF Core transaction, join it. The caller decides whether the
        // remediation commits, and the execution strategy is bypassed because retrying strategies cannot
        // replay work inside a caller-owned transaction.
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await RecordRemediationCoreAsync(
                    findingId,
                    actionCode,
                    actorId,
                    evidenceReference,
                    request.ResolveFinding,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        // Otherwise own the transaction inside the execution strategy so a retrying provider replays the
        // read, the guarded update, and the insert together.
        IExecutionStrategy executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(
                async strategyCancellationToken =>
                {
                    await using IDbContextTransaction transaction = await _dbContext.Database
                        .BeginTransactionAsync(strategyCancellationToken)
                        .ConfigureAwait(false);

                    ApplicationAuditReconciliationRemediationItem remediation = await RecordRemediationCoreAsync(
                            findingId,
                            actionCode,
                            actorId,
                            evidenceReference,
                            request.ResolveFinding,
                            strategyCancellationToken)
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
                    return remediation;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationCoreAsync(
        Guid findingId,
        string actionCode,
        string actorId,
        string? evidenceReference,
        bool resolveFinding,
        CancellationToken cancellationToken)
    {
        ApplicationAuditReconciliationFinding finding = await _dbContext
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == findingId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Audit reconciliation finding '{findingId}' was not found.");

        DateTime now = UtcNow();
        string remediationStatus = resolveFinding
            ? ApplicationAuditReconciliationRemediationStatuses.Resolved
            : ApplicationAuditReconciliationRemediationStatuses.Acknowledged;
        DateTime? resolvedUtc = resolveFinding ? now : null;
        string expectedFindingConcurrencyStamp = finding.ConcurrencyStamp;
        string nextFindingConcurrencyStamp = Guid.NewGuid().ToString("N");

        // Update the finding first, guarded by the stamp that was read. If a reconciliation run or another
        // remediation changed the finding in the meantime, no row matches and nothing is written.
        int updatedFindingCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            UPDATE [ApplicationAuditReconciliationFindings]
            SET [RemediationStatus] = {{remediationStatus}},
                [ResolvedUtc] = {{resolvedUtc}},
                [ConcurrencyStamp] = {{nextFindingConcurrencyStamp}}
            WHERE [Id] = {{findingId}}
                AND [ConcurrencyStamp] = {{expectedFindingConcurrencyStamp}}
            """, cancellationToken).ConfigureAwait(false);

        if (updatedFindingCount != 1)
        {
            throw new DbUpdateConcurrencyException(
                $"Audit reconciliation finding '{findingId}' was modified after it was read. " +
                "No remediation was recorded. Reload the finding, verify its current state, and retry.");
        }

        var remediationId = Guid.NewGuid();
        string remediationConcurrencyStamp = Guid.NewGuid().ToString("N");

        await _dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO [ApplicationAuditReconciliationRemediations]
                ([Id], [FindingId], [MutationBatchId], [ActionCode], [ActorId], [EvidenceReference], [RecordedUtc], [ConcurrencyStamp])
            VALUES
                ({{remediationId}}, {{findingId}}, {{finding.MutationBatchId}}, {{actionCode}}, {{actorId}}, {{evidenceReference}}, {{now}}, {{remediationConcurrencyStamp}})
            """, cancellationToken).ConfigureAwait(false);

        return new(
            remediationId,
            findingId,
            finding.MutationBatchId,
            actionCode,
            actorId,
            evidenceReference,
            now);
    }

    private List<ApplicationAuditReconciliationCandidate> BuildCandidates(
        IReadOnlyCollection<AuditRecord> auditRecords,
        IReadOnlyCollection<ApplicationAuditCompletionOutboxEntry> completionEntries,
        DateTime now)
    {
        var candidates = new Dictionary<string, ApplicationAuditReconciliationCandidate>(StringComparer.Ordinal);
        ILookup<string, AuditRecord> auditBatches = auditRecords
            .Where(record => !string.IsNullOrWhiteSpace(record.MutationBatchId))
            .ToLookup(record => record.MutationBatchId, StringComparer.Ordinal);
        ILookup<string, ApplicationAuditCompletionOutboxEntry> completionBatches = completionEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.MutationBatchId))
            .ToLookup(entry => entry.MutationBatchId, StringComparer.Ordinal);

        foreach (AuditRecord malformed in auditRecords.Where(record => string.IsNullOrWhiteSpace(record.MutationBatchId)))
        {
            Add(candidates, Candidate(
                ApplicationAuditReconciliationReasonCodes.MalformedCorrelation,
                ApplicationAuditReconciliationSeverities.Error,
                $"missing-{malformed.Id:N}",
                null,
                "Retain the row, investigate the originating save path, and append remediation evidence."));
        }

        foreach (IGrouping<string, AuditRecord> batch in auditBatches)
        {
            List<AuditRecord> records = [.. batch];
            List<ApplicationAuditCompletionOutboxEntry> completions = [.. completionBatches[batch.Key]];
            DateTime newestRecordUtc = records.Max(record => record.ModifiedOnUtc);

            if (completions.Count == 0 && newestRecordUtc <= now - _options.CompletionGracePeriod)
            {
                Add(candidates, Candidate(
                    ApplicationAuditReconciliationReasonCodes.MissingCompletion,
                    ApplicationAuditReconciliationSeverities.Critical,
                    batch.Key,
                    null,
                    "Preserve the audit batch, investigate transaction completion, and append an operator remediation record."));
            }

            foreach (ApplicationAuditCompletionOutboxEntry completion in completions)
            {
                if (completion.AuditRecordCount != records.Count)
                {
                    Add(candidates, Candidate(
                        ApplicationAuditReconciliationReasonCodes.AuditRecordCountMismatch,
                        ApplicationAuditReconciliationSeverities.Critical,
                        batch.Key,
                        completion.Destination,
                        "Do not rewrite audit rows; compare retained evidence with the originating transaction and document the discrepancy."));
                }

                ApplicationMutationAuditReceipt receipt = ToReceipt(completion);
                if (!_manifestVerifier.Verify(receipt, records))
                {
                    Add(candidates, Candidate(
                        ApplicationAuditReconciliationReasonCodes.ManifestVerificationFailed,
                        ApplicationAuditReconciliationSeverities.Critical,
                        batch.Key,
                        completion.Destination,
                        "Quarantine downstream use of the batch, preserve all records, and investigate unauthorized or incomplete mutation evidence."));
                }
            }

            if (records.Any(record => record.State == "Added" && string.IsNullOrWhiteSpace(record.KeyValues)))
            {
                Add(candidates, Candidate(
                    ApplicationAuditReconciliationReasonCodes.IncompleteGeneratedValues,
                    ApplicationAuditReconciliationSeverities.Error,
                    batch.Key,
                    null,
                    "Verify generated keys in the business database and append remediation evidence without modifying the retained audit row."));
            }

            if (HasMalformedCorrelation(records))
            {
                Add(candidates, Candidate(
                    ApplicationAuditReconciliationReasonCodes.MalformedCorrelation,
                    ApplicationAuditReconciliationSeverities.Warning,
                    batch.Key,
                    null,
                    "Investigate inconsistent correlation metadata and preserve the original records as evidence."));
            }
        }

        foreach (IGrouping<string, ApplicationAuditCompletionOutboxEntry> batch in completionBatches)
        {
            if (!auditBatches.Contains(batch.Key))
            {
                foreach (ApplicationAuditCompletionOutboxEntry completion in batch)
                {
                    Add(candidates, Candidate(
                        ApplicationAuditReconciliationReasonCodes.MissingAuditBatch,
                        ApplicationAuditReconciliationSeverities.Critical,
                        batch.Key,
                        completion.Destination,
                        "Preserve the completion record and investigate missing or externally stored audit evidence."));
                }
            }
        }

        foreach (IGrouping<(string MutationBatchId, string Destination), ApplicationAuditCompletionOutboxEntry> duplicate in
            completionEntries.GroupBy(entry => (entry.MutationBatchId, entry.Destination)))
        {
            if (duplicate.Count() > 1)
            {
                Add(candidates, Candidate(
                    ApplicationAuditReconciliationReasonCodes.DuplicateCompletion,
                    ApplicationAuditReconciliationSeverities.Critical,
                    duplicate.Key.MutationBatchId,
                    duplicate.Key.Destination,
                    "Preserve all records, stop dispatch for the destination, and investigate uniqueness or migration drift."));
            }
        }

        foreach (ApplicationAuditCompletionOutboxEntry entry in completionEntries)
        {
            AddDeliveryCandidate(candidates, entry, now);
        }

        return [.. candidates.Values];
    }

    private void AddDeliveryCandidate(
        IDictionary<string, ApplicationAuditReconciliationCandidate> candidates,
        ApplicationAuditCompletionOutboxEntry entry,
        DateTime now)
    {
        if ((entry.Status == ApplicationAuditCompletionOutboxStatuses.Pending ||
             entry.Status == ApplicationAuditCompletionOutboxStatuses.Deferred) &&
            entry.CreatedUtc <= now - _options.StalePendingThreshold)
        {
            Add(candidates, Candidate(
                ApplicationAuditReconciliationReasonCodes.StalePending,
                ApplicationAuditReconciliationSeverities.Warning,
                entry.MutationBatchId,
                entry.Destination,
                "Verify dispatcher availability and destination registration before retrying delivery."));
        }
        else if (entry.Status == ApplicationAuditCompletionOutboxStatuses.RetryableFailure &&
                 entry.NextAttemptUtc <= now - _options.StaleRetryReadyThreshold)
        {
            Add(candidates, Candidate(
                ApplicationAuditReconciliationReasonCodes.StaleRetryReady,
                ApplicationAuditReconciliationSeverities.Error,
                entry.MutationBatchId,
                entry.Destination,
                "Inspect destination availability and retry policy; preserve prior attempt diagnostics."));
        }
        else if (entry.Status == ApplicationAuditCompletionOutboxStatuses.Failed)
        {
            Add(candidates, Candidate(
                ApplicationAuditReconciliationReasonCodes.DeliveryFailed,
                ApplicationAuditReconciliationSeverities.Error,
                entry.MutationBatchId,
                entry.Destination,
                "Investigate the terminal delivery failure and append operator remediation evidence."));
        }
        else if (entry.Status == ApplicationAuditCompletionOutboxStatuses.DeadLettered)
        {
            Add(candidates, Candidate(
                ApplicationAuditReconciliationReasonCodes.DeadLettered,
                ApplicationAuditReconciliationSeverities.Critical,
                entry.MutationBatchId,
                entry.Destination,
                "Review the dead letter, preserve diagnostics, correct the destination, and explicitly requeue only under operator control."));
        }
    }

    private async Task PersistCandidatesAsync(
        IReadOnlyCollection<ApplicationAuditReconciliationCandidate> candidates,
        IReadOnlyCollection<string> auditBatchIds,
        IReadOnlyCollection<ApplicationAuditCompletionOutboxEntry> completionEntries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // When the caller already owns an EF Core transaction, join it and let the caller decide whether
        // the reconciliation writes commit.
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            await PersistCandidatesCoreAsync(candidates, auditBatchIds, completionEntries, now, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Otherwise persist every finding change in one transaction inside the execution strategy, so a run
        // applies all of its inserts and guarded updates or none of them, and a retrying provider replays
        // the reads together with the writes.
        IExecutionStrategy executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(
                async strategyCancellationToken =>
                {
                    await using IDbContextTransaction transaction = await _dbContext.Database
                        .BeginTransactionAsync(strategyCancellationToken)
                        .ConfigureAwait(false);

                    await PersistCandidatesCoreAsync(
                            candidates,
                            auditBatchIds,
                            completionEntries,
                            now,
                            strategyCancellationToken)
                        .ConfigureAwait(false);

                    await transaction.CommitAsync(strategyCancellationToken).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PersistCandidatesCoreAsync(
        IReadOnlyCollection<ApplicationAuditReconciliationCandidate> candidates,
        IReadOnlyCollection<string> auditBatchIds,
        IReadOnlyCollection<ApplicationAuditCompletionOutboxEntry> completionEntries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        string[] keys = [.. candidates.Select(candidate => candidate.FindingKey)];
        string[] scopeBatchIds = [.. auditBatchIds
            .Concat(completionEntries.Select(entry => entry.MutationBatchId))
            .Where(batchId => !string.IsNullOrWhiteSpace(batchId))
            .Distinct(StringComparer.Ordinal)];

        // Read the active and the resolvable findings in one round trip. Each row's stamp guards its update.
        List<ApplicationAuditReconciliationFinding> existing = await _dbContext
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .Where(finding => keys.Contains(finding.FindingKey) ||
                (scopeBatchIds.Contains(finding.MutationBatchId) &&
                    finding.RemediationStatus != ApplicationAuditReconciliationRemediationStatuses.Resolved))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingByKey = existing
            .ToDictionary(finding => finding.FindingKey, StringComparer.Ordinal);
        var activeKeys = new HashSet<string>(keys, StringComparer.Ordinal);
        var scopeBatchIdSet = new HashSet<string>(scopeBatchIds, StringComparer.Ordinal);

        foreach (ApplicationAuditReconciliationCandidate candidate in candidates)
        {
            if (existingByKey.TryGetValue(candidate.FindingKey, out ApplicationAuditReconciliationFinding? finding))
            {
                int updatedCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                    UPDATE [ApplicationAuditReconciliationFindings]
                    SET [Severity] = {{candidate.Severity}},
                        [Guidance] = {{candidate.Guidance}},
                        [LastObservedUtc] = {{now}},
                        [RemediationStatus] = {{ApplicationAuditReconciliationRemediationStatuses.Open}},
                        [ResolvedUtc] = {{(DateTime?)null}},
                        [ConcurrencyStamp] = {{Guid.NewGuid().ToString("N")}}
                    WHERE [Id] = {{finding.Id}}
                        AND [ConcurrencyStamp] = {{finding.ConcurrencyStamp}}
                    """, cancellationToken).ConfigureAwait(false);

                EnsureSingleRowWritten(updatedCount, candidate.FindingKey);
            }
            else
            {
                // Insert only when no row holds the key, so a run that interleaved and inserted the same finding
                // surfaces as a concurrency conflict (and a rollback) rather than a unique index violation.
                var id = Guid.NewGuid();
                int insertedCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                    INSERT INTO [ApplicationAuditReconciliationFindings]
                        ([Id], [SchemaVersion], [FindingKey], [ReasonCode], [Severity], [MutationBatchId], [Destination], [Guidance], [RemediationStatus], [FirstObservedUtc], [LastObservedUtc], [ResolvedUtc], [ConcurrencyStamp])
                    SELECT
                        {{id}}, {{ApplicationAuditReconciliationFinding.CurrentSchemaVersion}}, {{candidate.FindingKey}}, {{candidate.ReasonCode}}, {{candidate.Severity}}, {{candidate.MutationBatchId}}, {{candidate.Destination}}, {{candidate.Guidance}}, {{ApplicationAuditReconciliationRemediationStatuses.Open}}, {{now}}, {{now}}, {{(DateTime?)null}}, {{Guid.NewGuid().ToString("N")}}
                    WHERE NOT EXISTS (
                        SELECT 1 FROM [ApplicationAuditReconciliationFindings]
                        WHERE [FindingKey] = {{candidate.FindingKey}})
                    """, cancellationToken).ConfigureAwait(false);

                EnsureSingleRowWritten(insertedCount, candidate.FindingKey);
            }
        }

        foreach (ApplicationAuditReconciliationFinding finding in existing.Where(finding =>
                     !activeKeys.Contains(finding.FindingKey) &&
                     scopeBatchIdSet.Contains(finding.MutationBatchId) &&
                     finding.RemediationStatus != ApplicationAuditReconciliationRemediationStatuses.Resolved))
        {
            int resolvedCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                UPDATE [ApplicationAuditReconciliationFindings]
                SET [RemediationStatus] = {{ApplicationAuditReconciliationRemediationStatuses.Resolved}},
                    [ResolvedUtc] = {{now}},
                    [ConcurrencyStamp] = {{Guid.NewGuid().ToString("N")}}
                WHERE [Id] = {{finding.Id}}
                    AND [ConcurrencyStamp] = {{finding.ConcurrencyStamp}}
                """, cancellationToken).ConfigureAwait(false);

            EnsureSingleRowWritten(resolvedCount, finding.FindingKey);
        }
    }

    private static void EnsureSingleRowWritten(int affectedCount, string findingKey)
    {
        if (affectedCount != 1)
        {
            throw new DbUpdateConcurrencyException(
                $"Audit reconciliation finding '{findingKey}' was modified by another writer after it was read. " +
                "No reconciliation changes were committed; the next reconciliation run re-evaluates the finding.");
        }
    }

    private async Task<ApplicationAuditReconciliationSummary> GetSummaryCoreAsync(
        DateTime? lastRunUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<ApplicationAuditReconciliationFinding> open = _dbContext
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .Where(finding => finding.RemediationStatus != ApplicationAuditReconciliationRemediationStatuses.Resolved);

        long openCount = await open.LongCountAsync(cancellationToken).ConfigureAwait(false);
        long errorCount = await open.LongCountAsync(
            finding => finding.Severity == ApplicationAuditReconciliationSeverities.Error,
            cancellationToken).ConfigureAwait(false);
        long criticalCount = await open.LongCountAsync(
            finding => finding.Severity == ApplicationAuditReconciliationSeverities.Critical,
            cancellationToken).ConfigureAwait(false);
        long manifestFailures = await open.LongCountAsync(
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.ManifestVerificationFailed,
            cancellationToken).ConfigureAwait(false);
        long missingCompletion = await open.LongCountAsync(
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.MissingCompletion,
            cancellationToken).ConfigureAwait(false);
        long staleDelivery = await open.LongCountAsync(
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.StalePending ||
                finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.StaleRetryReady ||
                finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.DeliveryFailed,
            cancellationToken).ConfigureAwait(false);
        long deadLetters = await open.LongCountAsync(
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.DeadLettered,
            cancellationToken).ConfigureAwait(false);

        return new(
            true,
            lastRunUtc,
            openCount,
            errorCount,
            criticalCount,
            manifestFailures,
            missingCompletion,
            staleDelivery,
            deadLetters);
    }

    private static bool HasMalformedCorrelation(IReadOnlyCollection<AuditRecord> records)
    {
        return records.Any(record =>
                IsWhitespaceOnly(record.OperationExecutionId) ||
                IsWhitespaceOnly(record.ExecutionAttemptId) ||
                IsWhitespaceOnly(record.DecisionAuditRecordId) ||
                IsWhitespaceOnly(record.CorrelationId) ||
                IsWhitespaceOnly(record.TraceId)) ||
            HasMultipleValues(records.Select(record => record.OperationExecutionId)) ||
            HasMultipleValues(records.Select(record => record.ExecutionAttemptId)) ||
            HasMultipleValues(records.Select(record => record.DecisionAuditRecordId)) ||
            HasMultipleValues(records.Select(record => record.CorrelationId)) ||
            HasMultipleValues(records.Select(record => record.TraceId));
    }

    private static bool HasMultipleValues(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Skip(1)
            .Any();
    }

    private static bool IsWhitespaceOnly(string? value)
    {
        return value is not null && string.IsNullOrWhiteSpace(value);
    }

    private static ApplicationMutationAuditReceipt ToReceipt(ApplicationAuditCompletionOutboxEntry entry)
    {
        return new(
            entry.MutationBatchId,
            entry.AuditRecordCount,
            entry.PersistenceOutcome,
            new DateTimeOffset(DateTime.SpecifyKind(entry.ReceiptCompletedUtc, DateTimeKind.Utc)),
            entry.MutationManifestHash,
            entry.MutationManifestAlgorithm,
            entry.MutationManifestSchemaVersion,
            entry.OperationExecutionId,
            entry.ExecutionAttemptId,
            entry.DecisionAuditRecordId,
            entry.CorrelationId,
            entry.TraceId);
    }

    private static ApplicationAuditReconciliationCandidate Candidate(
        string reasonCode,
        string severity,
        string batchId,
        string? destination,
        string guidance)
    {
        string normalizedBatchId = NormalizeRequired(batchId, 64, nameof(batchId));
        string? normalizedDestination = NormalizeOptional(destination, 128);
        string identity = $"{reasonCode}\n{normalizedBatchId}\n{normalizedDestination}";
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new(key, reasonCode, severity, normalizedBatchId, normalizedDestination, guidance);
    }

    private static void Add(
        IDictionary<string, ApplicationAuditReconciliationCandidate> candidates,
        ApplicationAuditReconciliationCandidate candidate)
    {
        candidates[candidate.FindingKey] = candidate;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maximumLength} characters.");
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private DateTime UtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
