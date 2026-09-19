using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditedTransactionTests
{
    [Fact]
    public void Execute_SynchronousMutation_CommitsBusinessAndAuditRecords()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        using ApplicationDbContext context = CreateContext(connection, pipeline);
        context.Database.EnsureCreated();
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        ApplicationAuditedTransactionResult result = coordinator.Execute(
            dbContext => dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "sync-user")),
            isolationLevel: IsolationLevel.Serializable);

        Assert.True(result.CommittedByCoordinator);
        Assert.False(result.RequiresOuterCommit);
        Assert.False(result.UsedSavepoint);
        Assert.NotNull(result.AuditReceipt);
        Assert.Equal(1, context.ExternalLoginAccounts.Count());
        Assert.Equal(1, context.AuditRecords.Count());
    }

    [Fact]
    public async Task ExecuteAsync_LocalCompletionHandoff_CommitsAtomically()
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
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "handoff-user"));
                return Task.CompletedTask;
            },
            (dbContext, receipt, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.AuditRecords.Add(CreateCompletionRecord(receipt));
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.CommittedByCoordinator);
        ApplicationMutationAuditReceipt receipt = Assert.IsType<ApplicationMutationAuditReceipt>(result.AuditReceipt);
        Assert.True(result.MutationSaveChangesCount > 0);
        Assert.True(result.CompletionSaveChangesCount > 0);
        Assert.Equal(1, await context.ExternalLoginAccounts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.AuditRecords.CountAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken),
            record => record.Entity == "ApplicationAuditCompletion" &&
                record.MutationBatchId == receipt.MutationBatchId);
    }

    [Fact]
    public async Task ExecuteAsync_BusinessFailure_RollsBackTransaction()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "business-failure"));
                throw new InvalidOperationException("Business mutation failed.");
            },
            cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_CompletionHandoffFailure_RollsBackMutationAndAudit()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "handoff-failure"));
                return Task.CompletedTask;
            },
            (_, _, _) => throw new InvalidOperationException("Completion handoff failed."),
            cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedKeyAuditCompletion_CommitsCompletedKey()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline, useGeneratedKeyModel: true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        ApplicationAuditedTransactionResult result = await coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "generated-key"));
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        AuditRecord auditRecord = await context.AuditRecords.SingleAsync(TestContext.Current.CancellationToken);
        using var keyValues = JsonDocument.Parse(auditRecord.KeyValues);
        JsonElement generatedKey = keyValues.RootElement.GetProperty("GeneratedId");

        Assert.True(result.CommittedByCoordinator);
        Assert.True(generatedKey.GetInt32() > 0);
        Assert.Equal(auditRecord.MutationBatchId, result.AuditReceipt?.MutationBatchId);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedKeyAuditFailure_RollsBackBusinessMutation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline(new ThrowingApplicationAuditStore());
        await using ApplicationDbContext context = CreateContext(connection, pipeline, useGeneratedKeyModel: true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "generated-key-failure"));
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_ExistingTransaction_UsesSavepointAndLeavesCommitToCaller()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        await using IDbContextTransaction outerTransaction = await context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);

        ApplicationAuditedTransactionResult result = await coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "existing-transaction"));
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.RequiresOuterCommit);
        Assert.False(result.CommittedByCoordinator);
        Assert.True(result.UsedSavepoint);
        Assert.Equal(1, await context.ExternalLoginAccounts.CountAsync(TestContext.Current.CancellationToken));

        await outerTransaction.RollbackAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_ExistingTransactionFailure_RollsBackToCoordinatorSavepoint()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        await using IDbContextTransaction outerTransaction = await context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "savepoint-failure"));
                return Task.CompletedTask;
            },
            (_, _, _) => throw new InvalidOperationException("Local completion failed."),
            cancellationToken: TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(context.Database.CurrentTransaction);

        await outerTransaction.RollbackAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeStart_DoesNotInvokeMutation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        bool mutationInvoked = false;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ExecuteAsync(
            (_, _) =>
            {
                mutationInvoked = true;
                return Task.CompletedTask;
            },
            cancellationToken: cancellationSource.Token));

        Assert.False(mutationInvoked);
        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledAfterMutationSave_RollsBackTransaction()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        using var cancellationSource = new CancellationTokenSource();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "cancelled-after-save"));
                return Task.CompletedTask;
            },
            (_, _, cancellationToken) =>
            {
                cancellationSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            cancellationToken: cancellationSource.Token));

        context.ChangeTracker.Clear();
        Assert.Empty(await context.ExternalLoginAccounts.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.AuditRecords.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_RetryingExecutionStrategy_ReplaysWholeOwnedTransaction()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationSaveChangesPipeline pipeline = CreatePipeline();
        await using ApplicationDbContext context = CreateContext(
            connection,
            pipeline,
            useRetryingExecutionStrategy: true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var coordinator = new ApplicationAuditedTransaction(context, pipeline);
        int attempts = 0;

        ApplicationAuditedTransactionResult result = await coordinator.ExecuteAsync(
            (dbContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempts++;
                if (attempts == 1)
                {
                    throw new TestTransientException();
                }

                _ = dbContext.ExternalLoginAccounts.Add(CreateAccount("github", "retry-user"));
                return Task.CompletedTask;
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts);
        Assert.True(result.CommittedByCoordinator);
        Assert.Equal(1, await context.ExternalLoginAccounts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.AuditRecords.CountAsync(TestContext.Current.CancellationToken));
    }

    private static ApplicationSaveChangesPipeline CreatePipeline(IApplicationAuditStore? auditStore = null)
    {
        return new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(CreateDataAccessOptions()),
            auditStore);
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        ApplicationSaveChangesPipeline pipeline,
        bool useGeneratedKeyModel = false,
        bool useRetryingExecutionStrategy = false)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        _ = optionsBuilder.UseSqlite(connection, sqliteOptions =>
        {
            if (useRetryingExecutionStrategy)
            {
                _ = sqliteOptions.ExecutionStrategy(
                    dependencies => new TestRetryExecutionStrategy(dependencies));
            }
        });

        if (useGeneratedKeyModel)
        {
            _ = optionsBuilder.ReplaceService<IModelCustomizer, GeneratedKeyModelCustomizer>();
        }

        // create the interceptor wrapper expected by ApplicationDbContext
        var saveChangesInterceptor = new ApplicationSaveChangesInterceptor(pipeline);

        return new ApplicationDbContext(
            optionsBuilder.Options,
            NullLogger<ApplicationDbContext>.Instance,
            saveChangesInterceptor);
    }

    private static DataAccessOptions CreateDataAccessOptions()
    {
        return new DataAccessOptions
        {
            Auditing = new DataAuditingOptions
            {
                Enabled = true,
                StorageMode = AuditStorageModes.Local
            }
        };
    }

    private static ExternalLoginAccount CreateAccount(string providerName, string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = providerName,
            ProviderUserId = providerUserId,
            DisplayName = "Audited Transaction User",
            Email = $"{providerUserId}@example.com",
            CreatedOnUtc = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static AuditRecord CreateCompletionRecord(ApplicationMutationAuditReceipt receipt)
    {
        return new AuditRecord
        {
            ModifiedBy = "Application Audited Transaction Test",
            ActorId = "system",
            ActorType = ApplicationAuditActorTypes.System,
            Application = "ProjectTemplate.Web.Tests",
            Entity = "ApplicationAuditCompletion",
            State = "PendingDelivery",
            MutationBatchId = receipt.MutationBatchId,
            KeyValues = "{}",
            OriginalValues = "{}",
            CurrentValues = JsonSerializer.Serialize(new
            {
                receipt.MutationBatchId,
                receipt.MutationManifestHash,
                receipt.MutationManifestAlgorithm
            })
        };
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audited Transaction Test Actor";
    }

    private sealed class ThrowingApplicationAuditStore : IApplicationAuditStore
    {
        public void Append(ApplicationDbContext dbContext, AuditRecord auditRecord)
        {
            throw new InvalidOperationException("Generated-value audit persistence failed.");
        }

        public ValueTask AppendAsync(
            ApplicationDbContext dbContext,
            AuditRecord auditRecord,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(
                new InvalidOperationException("Generated-value audit persistence failed."));
        }
    }

    private sealed class GeneratedKeyModelCustomizer(ModelCustomizerDependencies dependencies) : IModelCustomizer
    {
        private readonly ModelCustomizer _defaultCustomizer = new(dependencies);

        public void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            _defaultCustomizer.Customize(modelBuilder, context);

            EntityTypeBuilder<ExternalLoginAccount> builder = modelBuilder.Entity<ExternalLoginAccount>();
            _ = builder.Property<int>("GeneratedId").ValueGeneratedOnAdd();
            _ = builder.HasKey("GeneratedId");
        }
    }

    private sealed class TestRetryExecutionStrategy(
        ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 1, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            return exception is TestTransientException;
        }
    }

    private sealed class TestTransientException : Exception
    {
    }
}
