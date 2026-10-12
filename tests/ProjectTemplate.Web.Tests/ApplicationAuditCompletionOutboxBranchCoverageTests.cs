using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the synchronous staging path, publisher registration guards, and publish-result branches of
/// <c>ApplicationAuditCompletionOutbox</c>.
/// </summary>
public sealed class ApplicationAuditCompletionOutboxBranchCoverageTests
{
    private const string _defaultDestination = "default";

    [Fact]
    public void Constructor_DuplicatePublisherDestination_ThrowsInvalidOperationException()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        using ApplicationDbContext context = CreateContext(connection);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateOutbox(
            context,
            [
                new StaticPublisher(_defaultDestination, ApplicationAuditCompletionPublishResult.Success()),
                new StaticPublisher(" DEFAULT ", ApplicationAuditCompletionPublishResult.Success())
            ]));

        Assert.Contains("More than one audit-completion publisher", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Constructor_PublisherWithoutDestination_ThrowsInvalidOperationException(string? destination)
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        using ApplicationDbContext context = CreateContext(connection);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateOutbox(
            context,
            [new StaticPublisher(destination!, ApplicationAuditCompletionPublishResult.Success())]));

        Assert.Contains("destination must be configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_PublisherDestinationTooLong_ThrowsInvalidOperationException()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        using ApplicationDbContext context = CreateContext(connection);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => CreateOutbox(
            context,
            [new StaticPublisher(new string('d', 129), ApplicationAuditCompletionPublishResult.Success())]));

        Assert.Contains("cannot exceed 128 characters", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_Disabled_ReturnsNullWithoutTrackingAnEntry()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        using ApplicationDbContext context = CreateContext(connection);
        context.Database.EnsureCreated();
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, [], options => options.Enabled = false);

        ApplicationAuditCompletionOutboxEntry? entry = outbox.Stage(context, CreateReceipt("sync-disabled-batch"));

        Assert.Null(entry);
        Assert.Empty(context.ApplicationAuditCompletionOutboxEntries.Local);
    }

    [Fact]
    public void Stage_RepeatedReceiptInSameContext_ReturnsTrackedEntry()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        using ApplicationDbContext context = CreateContext(connection);
        context.Database.EnsureCreated();
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        ApplicationMutationAuditReceipt receipt = CreateReceipt("sync-repeat-batch");

        ApplicationAuditCompletionOutboxEntry? first = outbox.Stage(context, receipt, " sync-destination ");
        ApplicationAuditCompletionOutboxEntry? second = outbox.Stage(context, receipt, "sync-destination");

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal("sync-destination", first.Destination);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.Pending, first.Status);
        Assert.Single(context.ApplicationAuditCompletionOutboxEntries.Local);
    }

    [Fact]
    public void Stage_ReceiptPersistedByAnotherContext_ReturnsPersistedEntry()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        ApplicationMutationAuditReceipt receipt = CreateReceipt("sync-persisted-batch");
        Guid persistedId;

        using (ApplicationDbContext stagingContext = CreateContext(connection))
        {
            stagingContext.Database.EnsureCreated();
            ApplicationAuditCompletionOutbox stagingOutbox = CreateOutbox(stagingContext, []);
            ApplicationAuditCompletionOutboxEntry staged = Assert.IsType<ApplicationAuditCompletionOutboxEntry>(
                stagingOutbox.Stage(stagingContext, receipt));
            persistedId = staged.Id;
            _ = stagingContext.SaveChanges();
        }

        using ApplicationDbContext context = CreateContext(connection);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);

        ApplicationAuditCompletionOutboxEntry? entry = outbox.Stage(context, receipt);

        Assert.NotNull(entry);
        Assert.Equal(persistedId, entry.Id);
        Assert.Equal(receipt.MutationBatchId, entry.MutationBatchId);
    }

    [Fact]
    public void Stage_ConflictingTrackedEntry_ThrowsInvalidOperationException()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        using ApplicationDbContext context = CreateContext(connection);
        context.Database.EnsureCreated();
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        _ = context.ApplicationAuditCompletionOutboxEntries.Add(new ApplicationAuditCompletionOutboxEntry
        {
            Id = CreateExpectedEntryId(_defaultDestination, "sync-conflict-batch"),
            Destination = _defaultDestination,
            IdempotencyKey = "conflicting-idempotency-key",
            MutationBatchId = "a-different-batch"
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            outbox.Stage(context, CreateReceipt("sync-conflict-batch")));

        Assert.Contains("conflicting persisted data", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchReadyAsync_PublisherThrows_SchedulesRetryWithSanitizedError()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [new ThrowingPublisher(new InvalidOperationException("line one\nline two"))],
            options => options.MaxErrorDetailLength = 8);
        await StageAndSaveAsync(outbox, context, "publisher-throws-batch");

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        ApplicationAuditCompletionOutboxEntry entry = await context.ApplicationAuditCompletionOutboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.RetryScheduledCount);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.RetryableFailure, entry.Status);
        Assert.Equal(1, entry.RetryCount);
        Assert.NotNull(entry.NextAttemptUtc);
        Assert.Equal(nameof(InvalidOperationException), entry.LastErrorCode);
        Assert.Equal("line one", entry.LastErrorMessage);
    }

    [Fact]
    public async Task DispatchReadyAsync_PublisherCancelsDispatch_PropagatesCancellation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        using var cancellationSource = new CancellationTokenSource();
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, [new CancellingPublisher(cancellationSource)]);
        await StageAndSaveAsync(outbox, context, "publisher-cancels-batch");

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            outbox.DispatchReadyAsync(cancellationSource.Token));

        context.ChangeTracker.Clear();
        ApplicationAuditCompletionOutboxEntry entry = await context.ApplicationAuditCompletionOutboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.Pending, entry.Status);
    }

    [Fact]
    public async Task DispatchReadyAsync_TerminalFailure_MarksEntryFailed()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [new StaticPublisher(
                _defaultDestination,
                ApplicationAuditCompletionPublishResult.TerminalFailure("rejected", "Rejected by destination."))]);
        await StageAndSaveAsync(outbox, context, "terminal-failure-batch");

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        ApplicationAuditCompletionOutboxEntry entry = await context.ApplicationAuditCompletionOutboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.Failed, entry.Status);
        Assert.Equal(1, entry.RetryCount);
        Assert.Null(entry.NextAttemptUtc);
        Assert.Equal("rejected", entry.LastErrorCode);
    }

    [Fact]
    public async Task DispatchReadyAsync_RetryWithPublisherDelay_UsesPublisherDelay()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [new StaticPublisher(
                _defaultDestination,
                ApplicationAuditCompletionPublishResult.Retry("busy", retryAfter: TimeSpan.FromMinutes(7)))]);
        await StageAndSaveAsync(outbox, context, "retry-after-batch");

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        ApplicationAuditCompletionOutboxEntry entry = await context.ApplicationAuditCompletionOutboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.RetryScheduledCount);
        Assert.Equal(entry.LastAttemptUtc + TimeSpan.FromMinutes(7), entry.NextAttemptUtc);
    }

    [Fact]
    public async Task DispatchReadyAsync_DeferWithPublisherDelay_UsesPublisherDelay()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [new StaticPublisher(
                _defaultDestination,
                ApplicationAuditCompletionPublishResult.Defer("paused", retryAfter: TimeSpan.FromMinutes(3)))]);
        await StageAndSaveAsync(outbox, context, "defer-after-batch");

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        ApplicationAuditCompletionOutboxEntry entry = await context.ApplicationAuditCompletionOutboxEntries
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, summary.DeferredCount);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.Deferred, entry.Status);
        Assert.Equal(entry.LastAttemptUtc + TimeSpan.FromMinutes(3), entry.NextAttemptUtc);
    }

    [Fact]
    public async Task DispatchReadyAsync_UnsupportedDisposition_ThrowsInvalidOperationException()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [new StaticPublisher(
                _defaultDestination,
                new ApplicationAuditCompletionPublishResult((ApplicationAuditCompletionPublishDisposition)99))]);
        await StageAndSaveAsync(outbox, context, "unsupported-disposition-batch");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            outbox.DispatchReadyAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Unsupported audit-completion publish disposition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetHealthAsync_EmptyBacklog_ReportsNoOldestPendingAge()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);

        ApplicationAuditCompletionOutboxHealth health = await outbox.GetHealthAsync(
            TestContext.Current.CancellationToken);

        Assert.True(health.Enabled);
        Assert.Equal(0, health.BacklogCount);
        Assert.Null(health.OldestPendingAge);
        Assert.Equal(0, health.DeadLetterCount);
    }

    private static async Task StageAndSaveAsync(
        ApplicationAuditCompletionOutbox outbox,
        ApplicationDbContext context,
        string mutationBatchId)
    {
        _ = await outbox.StageAsync(
            context,
            CreateReceipt(mutationBatchId),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Guid CreateExpectedEntryId(string destination, string mutationBatchId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{destination}\n{mutationBatchId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions { Enabled = false }
            }));
        var interceptor = new ApplicationSaveChangesInterceptor(pipeline);
        return new ApplicationDbContext(options, NullLogger<ApplicationDbContext>.Instance, interceptor);
    }

    private static ApplicationAuditCompletionOutbox CreateOutbox(
        ApplicationDbContext context,
        IEnumerable<IApplicationAuditCompletionPublisher> publishers,
        Action<ApplicationAuditCompletionOutboxOptions>? configure = null)
    {
        var options = new ApplicationAuditCompletionOutboxOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(1),
            DeferredRetryDelay = TimeSpan.FromMinutes(1)
        };
        configure?.Invoke(options);
        return new ApplicationAuditCompletionOutbox(
            context,
            Microsoft.Extensions.Options.Options.Create(options),
            publishers,
            TimeProvider.System);
    }

    private static ApplicationMutationAuditReceipt CreateReceipt(string mutationBatchId)
    {
        return new ApplicationMutationAuditReceipt(
            mutationBatchId,
            1,
            "Committed",
            DateTimeOffset.UtcNow,
            "ABCDEF",
            "SHA-256",
            "1.0",
            "operation-1",
            "attempt-1",
            "decision-1",
            "correlation-1",
            "trace-1");
    }

    private sealed class StaticPublisher(string destination, ApplicationAuditCompletionPublishResult result)
        : IApplicationAuditCompletionPublisher
    {
        public string Destination => destination;

        public ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
            ApplicationAuditCompletionMessage message,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingPublisher(Exception exception) : IApplicationAuditCompletionPublisher
    {
        public string Destination => _defaultDestination;

        public ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
            ApplicationAuditCompletionMessage message,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<ApplicationAuditCompletionPublishResult>(exception);
        }
    }

    private sealed class CancellingPublisher(CancellationTokenSource cancellationSource)
        : IApplicationAuditCompletionPublisher
    {
        public string Destination => _defaultDestination;

        public ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
            ApplicationAuditCompletionMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationSource.Cancel();
            return ValueTask.FromCanceled<ApplicationAuditCompletionPublishResult>(cancellationToken);
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audit Completion Outbox Branch Test Actor";
    }
}
