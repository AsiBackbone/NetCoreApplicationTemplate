using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the synchronous savepoint and completion-handoff paths, explicit asynchronous isolation levels, and the
/// environment guards of <c>ApplicationAuditedTransaction</c>.
/// </summary>
/// <remarks>
/// The non-relational provider guard, the savepoint-unsupported guard, and the "execution strategy completed without
/// a result" fallbacks are not reachable with the SQLite provider used by the test suite.
/// </remarks>
public sealed class ApplicationAuditedTransactionBranchCoverageTests
{
    [Fact]
    public void Execute_LocalCompletionHandoff_CommitsMutationAndCompletion()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        using ApplicationDbContext context = CreateContext(connection, pipeline);
        context.Database.EnsureCreated();
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        ApplicationAuditedTransactionResult result = coordinator.Execute(
            dbContext => dbContext.ExternalLoginAccounts.Add(CreateAccount("sync-handoff-user")),
            (dbContext, receipt) => dbContext.AuditRecords.Add(CreateCompletionRecord(receipt)));

        Assert.True(result.CommittedByCoordinator);
        Assert.NotNull(result.AuditReceipt);
        Assert.True(result.CompletionSaveChangesCount > 0);
        Assert.Equal(2, context.AuditRecords.Count());
    }

    [Fact]
    public void Execute_ExistingTransaction_UsesSavepointAndLeavesCommitToCaller()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        using ApplicationDbContext context = CreateContext(connection, pipeline);
        context.Database.EnsureCreated();
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        using IDbContextTransaction outerTransaction = context.Database.BeginTransaction();

        ApplicationAuditedTransactionResult result = coordinator.Execute(
            dbContext => dbContext.ExternalLoginAccounts.Add(CreateAccount("sync-existing-transaction")));

        Assert.True(result.RequiresOuterCommit);
        Assert.False(result.CommittedByCoordinator);
        Assert.True(result.UsedSavepoint);
        Assert.Equal(1, context.ExternalLoginAccounts.Count());

        outerTransaction.Rollback();
        context.ChangeTracker.Clear();
        Assert.Empty(context.ExternalLoginAccounts);
        Assert.Empty(context.AuditRecords);
    }

    [Fact]
    public void Execute_ExistingTransactionFailure_RollsBackToCoordinatorSavepoint()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        using ApplicationDbContext context = CreateContext(connection, pipeline);
        context.Database.EnsureCreated();
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        using IDbContextTransaction outerTransaction = context.Database.BeginTransaction();

        _ = Assert.Throws<InvalidOperationException>(() => coordinator.Execute(
            dbContext => dbContext.ExternalLoginAccounts.Add(CreateAccount("sync-savepoint-failure")),
            (_, _) => throw new InvalidOperationException("Local completion failed.")));

        context.ChangeTracker.Clear();
        Assert.Empty(context.ExternalLoginAccounts);
        Assert.Empty(context.AuditRecords);
        Assert.NotNull(context.Database.CurrentTransaction);

        outerTransaction.Rollback();
    }

    [Fact]
    public void Execute_AmbientTransactionScope_ThrowsNotSupportedException()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        using ApplicationDbContext context = CreateContext(connection, pipeline);
        context.Database.EnsureCreated();
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        bool mutationInvoked = false;

        using (new System.Transactions.TransactionScope())
        {
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => coordinator.Execute(
                _ => mutationInvoked = true));

            Assert.Contains("Ambient System.Transactions", exception.Message, StringComparison.Ordinal);
        }

        Assert.False(mutationInvoked);
    }

    [Fact]
    public async Task ExecuteAsync_LocalCompletionWithoutAuditReceipt_RollsBack()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ApplicationSaveChangesPipeline pipeline = CreatePipeline(auditingEnabled: false);
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        bool completionInvoked = false;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAsync(
                (dbContext, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("async-missing-receipt"));
                    return Task.CompletedTask;
                },
                (_, _, _) =>
                {
                    completionInvoked = true;
                    return Task.CompletedTask;
                },
                cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Contains("produced no new audit receipt", exception.Message, StringComparison.Ordinal);
        Assert.False(completionInvoked);
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitIsolationLevel_CommitsOwnedTransaction()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        ApplicationAuditedTransactionResult result = await coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("async-isolation-user"));
                return Task.CompletedTask;
            },
            isolationLevel: IsolationLevel.Serializable,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.CommittedByCoordinator);
        Assert.False(result.UsedSavepoint);
        Assert.Equal(1, await context.ExternalLoginAccounts.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_ExistingTransactionWithIsolationLevel_ThrowsBeforeMutation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        await using IDbContextTransaction outerTransaction = await context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);
        bool mutationInvoked = false;

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAsync(
                (_, _) =>
                {
                    mutationInvoked = true;
                    return Task.CompletedTask;
                },
                isolationLevel: IsolationLevel.ReadCommitted,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("isolation level cannot be supplied", exception.Message, StringComparison.Ordinal);
        Assert.False(mutationInvoked);

        await outerTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    private static ApplicationSaveChangesPipeline CreatePipeline(bool auditingEnabled = true)
    {
        return new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions
                {
                    Enabled = auditingEnabled,
                    StorageMode = AuditStorageModes.Local
                }
            }));
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        ApplicationSaveChangesPipeline pipeline)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            new ApplicationSaveChangesInterceptor(pipeline));
    }

    private static ExternalLoginAccount CreateAccount(string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "github",
            ProviderUserId = providerUserId,
            DisplayName = "Audited Transaction Branch User",
            Email = $"{providerUserId}@example.com",
            CreatedOnUtc = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static AuditRecord CreateCompletionRecord(ApplicationMutationAuditReceipt receipt)
    {
        return new AuditRecord
        {
            ModifiedBy = "Application Audited Transaction Branch Test",
            ActorId = "system",
            ActorType = ApplicationAuditActorTypes.System,
            Application = "ProjectTemplate.Web.Tests",
            Entity = "ApplicationAuditCompletion",
            State = "PendingDelivery",
            MutationBatchId = receipt.MutationBatchId,
            KeyValues = "{}",
            OriginalValues = "{}",
            CurrentValues = "{}"
        };
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audited Transaction Branch Test Actor";
    }
}
