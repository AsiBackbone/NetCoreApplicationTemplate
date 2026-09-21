using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditReconciliationTests
{
    private static readonly DateTime _now = new(2026, 7, 19, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReconcileAsync_CleanState_HasNoFindings()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AuditRecord record = CreateAuditRecord("clean-batch");
        database.Context.AuditRecords.Add(record);
        database.Context.ApplicationAuditCompletionOutboxEntries.Add(CreateCompletion(record));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationSummary summary = await database.Reconciler.ReconcileAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.OpenFindingCount);
        Assert.Empty(await database.Context.ApplicationAuditReconciliationFindings
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReconcileAsync_MissingCompletion_CreatesStableFinding()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.AuditRecords.Add(CreateAuditRecord("missing-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);
        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationFinding finding = Assert.Single(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ApplicationAuditReconciliationReasonCodes.MissingCompletion, finding.ReasonCode);
        Assert.Equal(ApplicationAuditReconciliationSeverities.Critical, finding.Severity);
    }

    [Fact]
    public async Task ReconcileAsync_CountMismatch_IsDetected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AuditRecord record = CreateAuditRecord("count-batch");
        ApplicationAuditCompletionOutboxEntry completion = CreateCompletion(record);
        completion.AuditRecordCount = 2;
        database.Context.AddRange(record, completion);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.Contains(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken),
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.AuditRecordCountMismatch);
    }

    [Fact]
    public async Task ReconcileAsync_HashMismatch_IsDetected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AuditRecord record = CreateAuditRecord("hash-batch");
        ApplicationAuditCompletionOutboxEntry completion = CreateCompletion(record);
        completion.MutationManifestHash = new string('0', 64);
        database.Context.AddRange(record, completion);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.Contains(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken),
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.ManifestVerificationFailed);
    }

    [Fact]
    public async Task ReconcileAsync_StalePending_IsVisible()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AuditRecord record = CreateAuditRecord("stale-batch");
        ApplicationAuditCompletionOutboxEntry completion = CreateCompletion(record);
        completion.CreatedUtc = _now.AddHours(-1);
        completion.Status = ApplicationAuditCompletionOutboxStatuses.Pending;
        database.Context.AddRange(record, completion);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationSummary summary = await database.Reconciler.ReconcileAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.StaleDeliveryCount);
        Assert.Contains(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken),
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.StalePending);
    }

    [Fact]
    public async Task ReconcileAsync_DuplicateCompletion_IsDetected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await database.Context.Database.ExecuteSqlRawAsync(
            "DROP INDEX IX_ApplicationAuditCompletionOutbox_Destination_MutationBatchId",
            TestContext.Current.CancellationToken);
        AuditRecord record = CreateAuditRecord("duplicate-batch");
        ApplicationAuditCompletionOutboxEntry first = CreateCompletion(record);
        ApplicationAuditCompletionOutboxEntry second = CreateCompletion(record);
        second.Id = Guid.NewGuid();
        second.IdempotencyKey = $"duplicate-{Guid.NewGuid():N}";
        database.Context.AddRange(record, first, second);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.Contains(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken),
            finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.DuplicateCompletion);
    }

    [Fact]
    public async Task ReconcileAsync_FindingChangedAfterRead_ThrowsConcurrencyExceptionAndRollsBackRun()
    {
        var interceptor = new NonQueryInterceptor();
        await using TestDatabase database = await TestDatabase.CreateAsync(interceptor: interceptor);
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "reconcile-concurrency-batch");
        database.Context.AuditRecords.Add(CreateAuditRecord("reconcile-concurrency-new-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        bool simulatedConcurrentWrite = false;
        interceptor.OnNonQueryExecuting = async (command, cancellationToken) =>
        {
            if (simulatedConcurrentWrite ||
                !command.CommandText.Contains("UPDATE [ApplicationAuditReconciliationFindings]", StringComparison.Ordinal))
            {
                return;
            }

            simulatedConcurrentWrite = true;

            // Simulate another reconciler run or remediation changing the finding after this run read it.
            await using DbCommand concurrentWrite = command.Connection!.CreateCommand();
            concurrentWrite.Transaction = command.Transaction;
            concurrentWrite.CommandText =
                "UPDATE [ApplicationAuditReconciliationFindings] SET [ConcurrencyStamp] = 'concurrent-writer'";
            _ = await concurrentWrite.ExecuteNonQueryAsync(cancellationToken);
        };

        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => database.Reconciler
            .ReconcileAsync(TestContext.Current.CancellationToken));

        interceptor.OnNonQueryExecuting = null;

        Assert.True(simulatedConcurrentWrite);
        Assert.Null(database.Context.Database.CurrentTransaction);
        ApplicationAuditReconciliationFinding current = Assert.Single(
            await database.Context.ApplicationAuditReconciliationFindings
                .AsNoTracking()
                .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(finding.Id, current.Id);
        Assert.Equal(finding.ConcurrencyStamp, current.ConcurrencyStamp);
    }

    [Fact]
    public async Task ReconcileAsync_ConcurrentRunInsertedSameFinding_ThrowsConcurrencyExceptionWithoutDuplicate()
    {
        var interceptor = new NonQueryInterceptor();
        await using TestDatabase database = await TestDatabase.CreateAsync(interceptor: interceptor);
        database.Context.AuditRecords.Add(CreateAuditRecord("reconcile-insert-race-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        bool simulatedConcurrentInsert = false;
        interceptor.OnNonQueryExecuting = async (command, cancellationToken) =>
        {
            if (simulatedConcurrentInsert ||
                !command.CommandText.Contains("INSERT INTO [ApplicationAuditReconciliationFindings]", StringComparison.Ordinal))
            {
                return;
            }

            simulatedConcurrentInsert = true;

            // Simulate an interleaved run inserting the same finding key between this run's read and insert.
            await using DbCommand concurrentInsert = command.Connection!.CreateCommand();
            concurrentInsert.Transaction = command.Transaction;
            concurrentInsert.CommandText = command.CommandText;
            foreach (DbParameter parameter in command.Parameters)
            {
                _ = concurrentInsert.Parameters.Add(new SqliteParameter(parameter.ParameterName, parameter.Value));
            }

            concurrentInsert.Parameters[0].Value = Guid.NewGuid().ToString().ToUpperInvariant();
            _ = await concurrentInsert.ExecuteNonQueryAsync(cancellationToken);
        };

        _ = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => database.Reconciler
            .ReconcileAsync(TestContext.Current.CancellationToken));

        interceptor.OnNonQueryExecuting = null;

        Assert.True(simulatedConcurrentInsert);
        Assert.Empty(await database.Context.ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReconcileAsync_CallerOwnedTransaction_JoinsTransactionWithoutCommitting()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.AuditRecords.Add(CreateAuditRecord("reconcile-caller-transaction-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using (IDbContextTransaction transaction = await database.Context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

            Assert.Same(transaction, database.Context.Database.CurrentTransaction);

            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await database.Context.ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordRemediationAsync_AppendsEvidenceAndResolvesFinding()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.AuditRecords.Add(CreateAuditRecord("remediation-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);
        ApplicationAuditReconciliationFinding finding = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationRemediationItem remediation = await database.Reconciler
            .RecordRemediationAsync(
                finding.Id,
                new("OperatorReviewed", "operator-1", "ticket-123", ResolveFinding: true),
                TestContext.Current.CancellationToken);

        Assert.Equal(finding.Id, remediation.FindingId);
        Assert.Single(await database.Context.ApplicationAuditReconciliationRemediations
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
        ApplicationAuditReconciliationFinding resolved = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationAuditReconciliationRemediationStatuses.Resolved, resolved.RemediationStatus);
        Assert.NotNull(resolved.ResolvedUtc);
    }

    [Fact]
    public async Task RecordRemediationAsync_InsertFails_RollsBackFindingUpdate()
    {
        var interceptor = new NonQueryInterceptor();
        await using TestDatabase database = await TestDatabase.CreateAsync(interceptor: interceptor);
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "rollback-batch");

        interceptor.OnNonQueryExecuting = (command, _) =>
            command.CommandText.Contains("INSERT INTO [ApplicationAuditReconciliationRemediations]", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Simulated remediation insert failure.")
                : Task.CompletedTask;

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Reconciler
            .RecordRemediationAsync(
                finding.Id,
                new("OperatorReviewed", "operator-1", "ticket-456", ResolveFinding: true),
                TestContext.Current.CancellationToken));

        interceptor.OnNonQueryExecuting = null;

        await AssertFindingUnchangedAsync(database, finding);
    }

    [Fact]
    public async Task RecordRemediationAsync_FindingChangedAfterRead_ThrowsConcurrencyExceptionAndWritesNothing()
    {
        var interceptor = new NonQueryInterceptor();
        await using TestDatabase database = await TestDatabase.CreateAsync(interceptor: interceptor);
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "concurrency-batch");

        bool simulatedConcurrentWrite = false;
        interceptor.OnNonQueryExecuting = async (command, cancellationToken) =>
        {
            if (simulatedConcurrentWrite ||
                !command.CommandText.Contains("UPDATE [ApplicationAuditReconciliationFindings]", StringComparison.Ordinal))
            {
                return;
            }

            simulatedConcurrentWrite = true;

            // Simulate another writer changing the finding between the remediation read and its guarded update.
            await using DbCommand concurrentWrite = command.Connection!.CreateCommand();
            concurrentWrite.Transaction = command.Transaction;
            concurrentWrite.CommandText =
                "UPDATE [ApplicationAuditReconciliationFindings] SET [ConcurrencyStamp] = 'concurrent-writer'";
            _ = await concurrentWrite.ExecuteNonQueryAsync(cancellationToken);
        };

        DbUpdateConcurrencyException exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => database.Reconciler
            .RecordRemediationAsync(
                finding.Id,
                new("OperatorReviewed", "operator-1", "ticket-789", ResolveFinding: true),
                TestContext.Current.CancellationToken));

        interceptor.OnNonQueryExecuting = null;

        Assert.True(simulatedConcurrentWrite);
        Assert.Contains(finding.Id.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Empty(await database.Context.ApplicationAuditReconciliationRemediations
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
        ApplicationAuditReconciliationFinding current = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationAuditReconciliationRemediationStatuses.Open, current.RemediationStatus);
        Assert.Null(current.ResolvedUtc);
    }

    [Fact]
    public async Task RecordRemediationAsync_CallerOwnedTransaction_JoinsTransactionWithoutCommitting()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "caller-transaction-batch");

        await using (IDbContextTransaction transaction = await database.Context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            _ = await database.Reconciler.RecordRemediationAsync(
                finding.Id,
                new("OperatorReviewed", "operator-1", "ticket-321", ResolveFinding: true),
                TestContext.Current.CancellationToken);

            Assert.Same(transaction, database.Context.Database.CurrentTransaction);

            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        await AssertFindingUnchangedAsync(database, finding);
    }

    [Fact]
    public async Task RecordRemediationAsync_InvalidRequest_ThrowsBeforeWriting()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        ApplicationAuditReconciliationFinding finding = await CreateOpenFindingAsync(database, "invalid-request-batch");

        _ = await Assert.ThrowsAsync<ArgumentException>(() => database.Reconciler
            .RecordRemediationAsync(
                finding.Id,
                new(" ", "operator-1"),
                TestContext.Current.CancellationToken));

        await AssertFindingUnchangedAsync(database, finding);
    }

    [Fact]
    public async Task ReconcileAsync_RecordsWithoutBatchId_AreBoundedPerRunNewestFirst()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(maximumMalformedRecordsPerRun: 2);
        var malformed = new List<AuditRecord>();
        for (int index = 0; index < 5; index++)
        {
            AuditRecord record = CreateAuditRecord(string.Empty);
            record.ModifiedOnUtc = _now.AddMinutes(-10 - index);
            malformed.Add(record);
        }

        database.Context.AuditRecords.AddRange(malformed);
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        List<string> findingKeys = await database.Context.ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .Where(finding => finding.ReasonCode == ApplicationAuditReconciliationReasonCodes.MalformedCorrelation)
            .Select(finding => finding.MutationBatchId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            malformed.Take(2).Select(record => $"missing-{record.Id:N}").Order(StringComparer.Ordinal),
            findingKeys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DisabledMode_DoesNotCreateFindings()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(enabled: false);
        database.Context.AuditRecords.Add(CreateAuditRecord("disabled-batch"));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationSummary summary = await database.Reconciler.ReconcileAsync(
            TestContext.Current.CancellationToken);

        Assert.False(summary.Enabled);
        Assert.Empty(await database.Context.ApplicationAuditReconciliationFindings
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<ApplicationAuditReconciliationFinding> CreateOpenFindingAsync(
        TestDatabase database,
        string batchId)
    {
        database.Context.AuditRecords.Add(CreateAuditRecord(batchId));
        _ = await database.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _ = await database.Reconciler.ReconcileAsync(TestContext.Current.CancellationToken);

        ApplicationAuditReconciliationFinding finding = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationAuditReconciliationRemediationStatuses.Open, finding.RemediationStatus);
        return finding;
    }

    private static async Task AssertFindingUnchangedAsync(
        TestDatabase database,
        ApplicationAuditReconciliationFinding original)
    {
        Assert.Empty(await database.Context.ApplicationAuditReconciliationRemediations
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));

        ApplicationAuditReconciliationFinding current = await database.Context
            .ApplicationAuditReconciliationFindings
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(original.RemediationStatus, current.RemediationStatus);
        Assert.Equal(original.ResolvedUtc, current.ResolvedUtc);
        Assert.Equal(original.ConcurrencyStamp, current.ConcurrencyStamp);
    }

    private static AuditRecord CreateAuditRecord(string batchId)
    {
        return new()
        {
            SchemaVersion = "1.0",
            ModifiedBy = "test",
            ActorId = "test",
            ActorType = "System",
            ModifiedOnUtc = _now.AddMinutes(-10),
            Application = "tests",
            Entity = "Example",
            State = "Modified",
            MutationBatchId = batchId,
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
        return new()
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

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(
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

        public static async Task<TestDatabase> CreateAsync(
            bool enabled = true,
            IInterceptor? interceptor = null,
            int maximumMalformedRecordsPerRun = 1_000)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            DbContextOptionsBuilder<ApplicationDbContext> dbOptionsBuilder =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection);
            if (interceptor is not null)
            {
                _ = dbOptionsBuilder.AddInterceptors(interceptor);
            }

            DbContextOptions<ApplicationDbContext> dbOptions = dbOptionsBuilder.Options;
            var pipeline = new ApplicationSaveChangesPipeline(
                new TestCurrentActorAccessor(),
                Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
                {
                    Auditing = new DataAuditingOptions { Enabled = false }
                }));

            var interceptorForSavePipeline = new ApplicationSaveChangesInterceptor(pipeline);

            var context = new ApplicationDbContext(
                dbOptions,
                NullLogger<ApplicationDbContext>.Instance,
                interceptorForSavePipeline);
            _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var builder = new CanonicalApplicationMutationManifestBuilder();
            var hasher = new Sha256ApplicationMutationManifestHasher();
            var verifier = new ApplicationMutationManifestVerifier(builder, hasher);
            var metrics = new ApplicationAuditReconciliationMetrics();
            var reconciler = new ApplicationAuditReconciler(
                context,
                verifier,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions
                {
                    Enabled = enabled,
                    CompletionGracePeriod = TimeSpan.Zero,
                    StalePendingThreshold = TimeSpan.FromMinutes(15),
                    StaleRetryReadyThreshold = TimeSpan.FromMinutes(15),
                    MaximumMalformedRecordsPerRun = maximumMalformedRecordsPerRun
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

    private sealed class NonQueryInterceptor : DbCommandInterceptor
    {
        public Func<DbCommand, CancellationToken, Task>? OnNonQueryExecuting { get; set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Func<DbCommand, CancellationToken, Task>? callback = OnNonQueryExecuting;
            if (callback is not null)
            {
                await callback(command, cancellationToken);
            }

            return result;
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
        public string CurrentActor => "Audit Reconciliation Test Actor";
    }
}
