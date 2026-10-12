using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the constructor guards, delivery-state findings, record-shape findings, and remediation input branches of
/// <c>ApplicationAuditReconciler</c>.
/// </summary>
public sealed class ApplicationAuditReconcilerBranchCoverageTests
{
    private static readonly DateTime _now = new(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Constructor_NullDependency_ThrowsArgumentNullException()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        var verifier = new ApplicationMutationManifestVerifier(
            new CanonicalApplicationMutationManifestBuilder(),
            new Sha256ApplicationMutationManifestHasher());

        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciler(
            null!,
            verifier,
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
            database.Metrics,
            TimeProvider.System));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciler(
            database.Context,
            null!,
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
            database.Metrics,
            TimeProvider.System));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciler(
            database.Context,
            verifier,
            null!,
            database.Metrics,
            TimeProvider.System));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciler(
            database.Context,
            verifier,
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
            null!,
            TimeProvider.System));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciler(
            database.Context,
            verifier,
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
            database.Metrics,
            null!));
    }

    [Fact]
    public async Task ReconcileAsync_RetryFailedAndDeadLetteredDeliveries_CreateDeliveryFindings()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        AddBatchWithCompletion(
            database,
            "stale-retry-batch",
            ApplicationAuditCompletionOutboxStatuses.RetryableFailure,
            nextAttemptUtc: _now.AddHours(-1));
        AddBatchWithCompletion(
            database,
            "fresh-retry-batch",
            ApplicationAuditCompletionOutboxStatuses.RetryableFailure,
            nextAttemptUtc: _now.AddMinutes(5));
        AddBatchWithCompletion(database, "failed-delivery-batch", ApplicationAuditCompletionOutboxStatuses.Failed);
        AddBatchWithCompletion(database, "dead-letter-batch", ApplicationAuditCompletionOutboxStatuses.DeadLettered);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationSummary summary = await database.Reconciler.ReconcileAsync(
            TestContext.Current.CancellationToken);

        List<ApplicationAuditReconciliationFinding> findings = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, summary.StaleDeliveryCount);
        Assert.Equal(1, summary.DeadLetterCount);
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.StaleRetryReady &&
            finding.MutationBatchId == "stale-retry-batch");
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.DeliveryFailed &&
            finding.MutationBatchId == "failed-delivery-batch");
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.DeadLettered &&
            finding.MutationBatchId == "dead-letter-batch");
        Assert.DoesNotContain(findings, finding => finding.MutationBatchId == "fresh-retry-batch");
    }

    [Fact]
    public async Task ReconcileAsync_RecordShapeProblems_CreateGeneratedValueAndCorrelationFindings()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        AuditRecord addedWithoutKeys = CreateAuditRecord("generated-values-batch");
        addedWithoutKeys.State = "Added";
        addedWithoutKeys.KeyValues = string.Empty;
        AuditRecord firstCorrelation = CreateAuditRecord("conflicting-correlation-batch");
        firstCorrelation.CorrelationId = "correlation-a";
        AuditRecord secondCorrelation = CreateAuditRecord("conflicting-correlation-batch");
        secondCorrelation.CorrelationId = "correlation-b";
        AuditRecord whitespaceTrace = CreateAuditRecord("whitespace-trace-batch");
        whitespaceTrace.TraceId = " ";
        database.Context.AuditRecords.AddRange(addedWithoutKeys, firstCorrelation, secondCorrelation, whitespaceTrace);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        List<ApplicationAuditReconciliationFinding> findings = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.IncompleteGeneratedValues &&
            finding.MutationBatchId == "generated-values-batch");
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.MalformedCorrelation &&
            finding.MutationBatchId == "conflicting-correlation-batch");
        Assert.Contains(findings, finding =>
            finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.MalformedCorrelation &&
            finding.MutationBatchId == "whitespace-trace-batch");
    }

    [Fact]
    public async Task ReconcileAsync_CompletionWithoutAuditBatch_CreatesMissingAuditBatchFinding()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        ApplicationAuditCompletionOutboxEntry orphan = CreateCompletion(CreateAuditRecord("orphan-completion-batch"));
        orphan.Status = ApplicationAuditCompletionOutboxStatuses.Pending;
        orphan.CreatedUtc = _now.AddMinutes(-1);
        orphan.DeliveredUtc = null;
        database.Context.ApplicationAuditCompletionOutboxEntries.Add(orphan);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationSummary summary = await database.Reconciler.ReconcileAsync(
            TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationFinding finding = Assert.Single(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ApplicationAuditReconciliationReasonCodes.MissingAuditBatch, finding.ReasonCode);
        Assert.Equal("orphan-completion-batch", finding.MutationBatchId);
        Assert.Equal("default", finding.Destination);
        Assert.Equal(1, summary.CriticalFindingCount);
    }

    [Fact]
    public async Task RecordRemediationAsync_AcknowledgeWithLongEvidence_TruncatesEvidenceAndLeavesFindingUnresolved()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "acknowledge-batch");

        ApplicationAuditReconciliationRemediationItem remediation = await database.Reconciler
            .RecordRemediationAsync(
                finding.Id,
                new(" OperatorReviewed ", " operator-1 ", new string('e', 300)),
                TestContext.Current.CancellationToken);

        Assert.Equal("OperatorReviewed", remediation.ActionCode);
        Assert.Equal("operator-1", remediation.ActorId);
        Assert.Equal(256, remediation.EvidenceReference?.Length);
        ApplicationAuditReconciliationFinding acknowledged = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationAuditReconciliationRemediationStatuses.Acknowledged, acknowledged.RemediationStatus);
        Assert.Null(acknowledged.ResolvedUtc);
    }

    [Fact]
    public async Task RecordRemediationAsync_UnknownFinding_ThrowsKeyNotFoundException()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => database.Reconciler.RecordRemediationAsync(
            Guid.NewGuid(),
            new("OperatorReviewed", "operator-1"),
            TestContext.Current.CancellationToken));

        Assert.Empty(await database.Context.ApplicationAuditReconciliationRemediations
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordRemediationAsync_ActionCodeTooLong_ThrowsArgumentOutOfRangeException()
    {
        await using ReconcilerDatabase database = await ReconcilerDatabase.CreateAsync();
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "oversized-action-batch");

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.Reconciler.RecordRemediationAsync(
            finding.Id,
            new(new string('a', 65), "operator-1"),
            TestContext.Current.CancellationToken));

        Assert.Empty(await database.Context.ApplicationAuditReconciliationRemediations
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static void AddBatchWithCompletion(
        ReconcilerDatabase database,
        string mutationBatchId,
        string completionStatus,
        DateTime? nextAttemptUtc = null)
    {
        AuditRecord record = CreateAuditRecord(mutationBatchId);
        ApplicationAuditCompletionOutboxEntry completion = CreateCompletion(record);
        completion.Status = completionStatus;
        completion.NextAttemptUtc = nextAttemptUtc;
        completion.DeliveredUtc = null;
        database.Context.AddRange(record, completion);
    }

    private static async Task<ApplicationAuditReconciliationFinding> CreateOpenFindingAsync(
        ReconcilerDatabase database,
        string mutationBatchId)
    {
        database.Context.AuditRecords.Add(CreateAuditRecord(mutationBatchId));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        return await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static AuditRecord CreateAuditRecord(string mutationBatchId)
    {
        return new AuditRecord
        {
            SchemaVersion = "1.0",
            ModifiedBy = "test",
            ActorId = "test",
            ActorType = "System",
            ModifiedOnUtc = _now.AddMinutes(-10),
            Application = "tests",
            Entity = "Example",
            State = "Modified",
            MutationBatchId = mutationBatchId,
            KeyValues = /*lang=json,strict*/ "{\"Id\":\"1\"}",
            OriginalValues = /*lang=json,strict*/ "{\"Value\":\"before\"}",
            CurrentValues = /*lang=json,strict*/ "{\"Value\":\"after\"}"
        };
    }

    private static ApplicationAuditCompletionOutboxEntry CreateCompletion(AuditRecord record)
    {
        var builder = new CanonicalApplicationMutationManifestBuilder();
        var hasher = new Sha256ApplicationMutationManifestHasher();
        ApplicationMutationManifest manifest = builder.Build([record]);
        return new ApplicationAuditCompletionOutboxEntry
        {
            Id = Guid.NewGuid(),
            SchemaVersion = ApplicationAuditCompletionOutboxEntry.CurrentSchemaVersion,
            Destination = "default",
            IdempotencyKey = $"completion-{Guid.NewGuid():N}",
            MutationBatchId = record.MutationBatchId,
            AuditRecordCount = 1,
            PersistenceOutcome = "Committed",
            ReceiptCompletedUtc = _now.AddMinutes(-9),
            MutationManifestHash = hasher.ComputeHash(manifest),
            MutationManifestAlgorithm = hasher.Algorithm,
            MutationManifestSchemaVersion = manifest.SchemaVersion,
            Status = ApplicationAuditCompletionOutboxStatuses.Delivered,
            CreatedUtc = _now.AddMinutes(-9),
            DeliveredUtc = _now.AddMinutes(-8)
        };
    }

    private sealed class ReconcilerDatabase : IAsyncDisposable
    {
        private ReconcilerDatabase(
            SqliteConnection connection,
            ApplicationDbContext context,
            ApplicationAuditReconciler reconciler,
            ApplicationAuditReconciliationMetrics metrics)
        {
            Connection = connection;
            Context = context;
            Reconciler = reconciler;
            Metrics = metrics;
        }

        public SqliteConnection Connection { get; }

        public ApplicationDbContext Context { get; }

        public ApplicationAuditReconciler Reconciler { get; }

        public ApplicationAuditReconciliationMetrics Metrics { get; }

        public static async Task<ReconcilerDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            DbContextOptions<ApplicationDbContext> dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var pipeline = new ApplicationSaveChangesPipeline(
                new TestCurrentActorAccessor(),
                Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
                {
                    Auditing = new DataAuditingOptions { Enabled = false }
                }));
            var context = new ApplicationDbContext(
                dbOptions,
                NullLogger<ApplicationDbContext>.Instance,
                new ApplicationSaveChangesInterceptor(pipeline));
            _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var verifier = new ApplicationMutationManifestVerifier(
                new CanonicalApplicationMutationManifestBuilder(),
                new Sha256ApplicationMutationManifestHasher());
            var metrics = new ApplicationAuditReconciliationMetrics();
            var reconciler = new ApplicationAuditReconciler(
                context,
                verifier,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions
                {
                    CompletionGracePeriod = TimeSpan.Zero,
                    StalePendingThreshold = TimeSpan.FromMinutes(15),
                    StaleRetryReadyThreshold = TimeSpan.FromMinutes(15)
                }),
                metrics,
                new FixedTimeProvider(_now));
            return new(connection, context, reconciler, metrics);
        }

        public async ValueTask DisposeAsync()
        {
            Metrics.Dispose();
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow);

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audit Reconciler Branch Test Actor";
    }
}
