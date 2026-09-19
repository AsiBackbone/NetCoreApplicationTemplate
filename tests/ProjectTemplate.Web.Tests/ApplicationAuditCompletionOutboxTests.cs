using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditCompletionOutboxTests
{
    [Fact]
    public async Task StageAsync_PersistsAcrossContextRestart_AndDispatches()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ApplicationMutationAuditReceipt receipt = CreateReceipt("restart-batch");

        await using (ApplicationDbContext stagingContext = CreateContext(connection))
        {
            _ = await stagingContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            ApplicationAuditCompletionOutbox outbox = CreateOutbox(stagingContext, []);
            _ = await outbox.StageAsync(stagingContext, receipt, cancellationToken: TestContext.Current.CancellationToken);
            _ = await stagingContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var publisher = new RecordingPublisher(ApplicationAuditCompletionPublishResult.Success());
        await using ApplicationDbContext dispatchContext = CreateContext(connection);
        ApplicationAuditCompletionOutbox dispatcher = CreateOutbox(dispatchContext, [publisher]);

        ApplicationAuditCompletionDispatchSummary summary = await dispatcher.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.DeliveredCount);
        ApplicationAuditCompletionMessage message = Assert.Single(publisher.Messages);
        Assert.Equal(receipt.MutationBatchId, message.MutationBatchId);
        Assert.Equal(1, await dispatchContext.ApplicationAuditCompletionOutboxEntries
            .CountAsync(entry => entry.Status == ApplicationAuditCompletionOutboxStatuses.Delivered,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StageAsync_DuplicateReceiptAndDestination_ReturnsSameEntry()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        ApplicationMutationAuditReceipt receipt = CreateReceipt("duplicate-batch");

        ApplicationAuditCompletionOutboxEntry? first = await outbox.StageAsync(context, receipt, cancellationToken: TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutboxEntry? second = await outbox.StageAsync(context, receipt, cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Single(await context.ApplicationAuditCompletionOutboxEntries
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DispatchReadyAsync_RetryableFailures_DeadLetterAtConfiguredLimit()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var publisher = new RecordingPublisher(ApplicationAuditCompletionPublishResult.Retry("temporary"));
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [publisher],
            options =>
            {
                options.MaxRetryAttempts = 2;
                options.BaseRetryDelay = TimeSpan.FromTicks(1);
                options.MaxRetryDelay = TimeSpan.FromTicks(1);
            });
        _ = await outbox.StageAsync(context, CreateReceipt("retry-batch"),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditCompletionDispatchSummary first = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);
        await Task.Delay(1, TestContext.Current.CancellationToken);
        ApplicationAuditCompletionDispatchSummary second = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, first.RetryScheduledCount);
        Assert.Equal(1, second.DeadLetteredCount);
        Assert.Equal(2, publisher.Messages.Count);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.DeadLettered,
            (await context.ApplicationAuditCompletionOutboxEntries.SingleAsync(
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task DispatchReadyAsync_NoPublisher_DefersWithoutFailingHost()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        _ = await outbox.StageAsync(context, CreateReceipt("deferred-batch"),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        ApplicationAuditCompletionDispatchSummary summary = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.DeferredCount);
        Assert.Equal(ApplicationAuditCompletionOutboxStatuses.Deferred,
            (await context.ApplicationAuditCompletionOutboxEntries.SingleAsync(
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task DispatchReadyAsync_Cancelled_DoesNotInvokePublisher()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var publisher = new RecordingPublisher(ApplicationAuditCompletionPublishResult.Success());
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, [publisher]);
        _ = await outbox.StageAsync(context, CreateReceipt("cancel-batch"),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            outbox.DispatchReadyAsync(cancellationSource.Token));

        Assert.Empty(publisher.Messages);
    }

    [Fact]
    public async Task DisabledMode_DoesNotStageDispatchOrExposeBacklog()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var publisher = new RecordingPublisher(ApplicationAuditCompletionPublishResult.Success());
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(
            context,
            [publisher],
            options => options.Enabled = false);

        ApplicationAuditCompletionOutboxEntry? entry = await outbox.StageAsync(context, CreateReceipt("disabled-batch"),
            cancellationToken: TestContext.Current.CancellationToken);
        ApplicationAuditCompletionDispatchSummary dispatch = await outbox.DispatchReadyAsync(
            TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutboxHealth health = await outbox.GetHealthAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(entry);
        Assert.False(dispatch.Enabled);
        Assert.False(health.Enabled);
        Assert.Empty(publisher.Messages);
        Assert.Empty(await context.ApplicationAuditCompletionOutboxEntries
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHealthAsync_ReportsBacklogRetriesOldestAgeAndDeadLetters()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using ApplicationDbContext context = CreateContext(connection);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        ApplicationAuditCompletionOutbox outbox = CreateOutbox(context, []);
        _ = await outbox.StageAsync(context, CreateReceipt("health-batch"),
            cancellationToken: TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _ = await outbox.DispatchReadyAsync(TestContext.Current.CancellationToken);

        ApplicationAuditCompletionOutboxHealth health = await outbox.GetHealthAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(1, health.BacklogCount);
        Assert.NotNull(health.OldestPendingAge);
        Assert.Equal(0, health.TotalRetryCount);
        Assert.Equal(0, health.DeadLetterCount);
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

    private sealed class RecordingPublisher(ApplicationAuditCompletionPublishResult result)
        : IApplicationAuditCompletionPublisher
    {
        public string Destination => "default";

        public List<ApplicationAuditCompletionMessage> Messages { get; } = [];

        public ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
            ApplicationAuditCompletionMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audit Completion Outbox Test Actor";
    }
}
