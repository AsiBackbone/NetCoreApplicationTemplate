using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationSaveChangesInterceptorBranchCoverageTests
{
    [Fact]
    public void Constructor_WithNullPipeline_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplicationSaveChangesInterceptor(null!));
    }

    [Fact]
    public async Task SaveChanges_WithNonApplicationDbContext_DoesNotInvokePipeline()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        BranchTrackingSaveChangesPipeline saveChangesPipeline = new();
        ApplicationSaveChangesInterceptor interceptor = new(saveChangesPipeline);

        DbContextOptions<NonApplicationDbContext> options = new DbContextOptionsBuilder<NonApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using NonApplicationDbContext context = new(options);

        _ = context.Database.EnsureCreated();

        context.Entities.Add(new NonApplicationEntity { Name = "Non-Application Context" });

        int result = context.SaveChanges();

        Assert.Equal(1, result);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNonApplicationDbContext_DoesNotInvokePipeline()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        BranchTrackingSaveChangesPipeline saveChangesPipeline = new();
        ApplicationSaveChangesInterceptor interceptor = new(saveChangesPipeline);

        DbContextOptions<NonApplicationDbContext> options = new DbContextOptionsBuilder<NonApplicationDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using NonApplicationDbContext context = new(options);

        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.Entities.AddAsync(
            new NonApplicationEntity { Name = "Non-Application Context Async" },
            TestContext.Current.CancellationToken);

        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    [Fact]
    public async Task SaveChanges_WhenAfterPipelineRequestsAdditionalSave_RunsNestedSaveOnce()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        BranchTrackingSaveChangesPipeline saveChangesPipeline = new(afterSaveReturnsTrueOnce: true);

        await using ApplicationDbContext context = CreateApplicationContext(
            connection,
            saveChangesPipeline);

        _ = context.Database.EnsureCreated();

        context.ExternalLoginAccounts.Add(CreatePersistableAccount("after-save-sync-user"));

        int result = context.SaveChanges();

        Assert.Equal(1, result);
        Assert.Equal(2, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(2, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenAfterPipelineRequestsAdditionalSave_RunsNestedSaveOnce()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        BranchTrackingSaveChangesPipeline saveChangesPipeline = new(afterSaveReturnsTrueOnce: true);

        await using ApplicationDbContext context = CreateApplicationContext(
            connection,
            saveChangesPipeline);

        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.ExternalLoginAccounts.AddAsync(
            CreatePersistableAccount("after-save-async-user"),
            TestContext.Current.CancellationToken);

        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(2, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(2, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");

        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static ApplicationDbContext CreateApplicationContext(
        SqliteConnection connection,
        IApplicationSaveChangesPipeline saveChangesPipeline)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var interceptorForSavePipeline = new ApplicationSaveChangesInterceptor(saveChangesPipeline);

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            interceptorForSavePipeline);
    }

    private static ExternalLoginAccount CreatePersistableAccount(string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            NormalizedProviderName = "GITHUB",
            ProviderUserId = providerUserId,
            DisplayName = "Interceptor Branch User",
            Email = "interceptor.branch@example.com",
            NormalizedEmail = "INTERCEPTOR.BRANCH@EXAMPLE.COM",
            CreatedOnUtc = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class BranchTrackingSaveChangesPipeline(
        bool afterSaveReturnsTrueOnce = false) : IApplicationSaveChangesPipeline
    {
        private bool _afterSaveShouldReturnTrue = afterSaveReturnsTrueOnce;

        public int BeforeSaveChangesCallCount { get; private set; }

        public int BeforeSaveChangesAsyncCallCount { get; private set; }

        public int AfterSaveChangesCallCount { get; private set; }

        public int AfterSaveChangesAsyncCallCount { get; private set; }

        public bool ApplyBeforeSaveChanges(ApplicationDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            BeforeSaveChangesCallCount++;

            return true;
        }

        public ValueTask<bool> ApplyBeforeSaveChangesAsync(
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            cancellationToken.ThrowIfCancellationRequested();

            BeforeSaveChangesAsyncCallCount++;

            return ValueTask.FromResult(true);
        }

        public bool ApplyAfterSaveChanges(ApplicationDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            AfterSaveChangesCallCount++;

            return ReturnTrueOnceWhenConfigured();
        }

        public ValueTask<bool> ApplyAfterSaveChangesAsync(
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            cancellationToken.ThrowIfCancellationRequested();

            AfterSaveChangesAsyncCallCount++;

            return ValueTask.FromResult(ReturnTrueOnceWhenConfigured());
        }

        private bool ReturnTrueOnceWhenConfigured()
        {
            if (!_afterSaveShouldReturnTrue)
            {
                return false;
            }

            _afterSaveShouldReturnTrue = false;
            return true;
        }
    }

    private sealed class NonApplicationDbContext(DbContextOptions<NonApplicationDbContext> options)
        : DbContext(options)
    {
        public DbSet<NonApplicationEntity> Entities => Set<NonApplicationEntity>();
    }

    private sealed class NonApplicationEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
