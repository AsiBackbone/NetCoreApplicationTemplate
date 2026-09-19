using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextSavePipelineTests
{
    [Fact]
    public async Task SaveChanges_WithPendingChanges_InvokesInjectedPipeline()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        TrackingSaveChangesPipeline saveChangesPipeline = new();

        await using ApplicationDbContext context = CreateContext(
            connection,
            saveChangesPipeline);

        _ = context.Database.EnsureCreated();

        context.ExternalLoginAccounts.Add(CreatePersistableAccount("pipeline-sync-user"));

        int result = context.SaveChanges();

        Assert.Equal(1, result);
        Assert.Equal(1, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(1, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    [Fact]
    public async Task SaveChangesAsync_WithPendingChanges_InvokesInjectedPipeline()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        TrackingSaveChangesPipeline saveChangesPipeline = new();

        await using ApplicationDbContext context = CreateContext(
            connection,
            saveChangesPipeline);

        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.ExternalLoginAccounts.AddAsync(
            CreatePersistableAccount("pipeline-async-user"),
            TestContext.Current.CancellationToken);

        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
        Assert.Equal(0, saveChangesPipeline.BeforeSaveChangesCallCount);
        Assert.Equal(1, saveChangesPipeline.BeforeSaveChangesAsyncCallCount);
        Assert.Equal(0, saveChangesPipeline.AfterSaveChangesCallCount);
        Assert.Equal(1, saveChangesPipeline.AfterSaveChangesAsyncCallCount);
    }

    [Fact]
    public async Task Constructor_WithNullSaveChangesInterceptor_ThrowsArgumentNullException()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            saveChangesInterceptor: null!));

        Assert.Equal("saveChangesInterceptor", exception.ParamName);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");

        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static ExternalLoginAccount CreatePersistableAccount(string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            NormalizedProviderName = "GITHUB",
            ProviderUserId = providerUserId,
            DisplayName = "Pipeline User",
            Email = "pipeline.user@example.com",
            NormalizedEmail = "PIPELINE.USER@EXAMPLE.COM",
            CreatedOnUtc = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        IApplicationSaveChangesPipeline saveChangesPipeline)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        ApplicationSaveChangesInterceptor interceptor = new(saveChangesPipeline);

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            interceptor);
    }

    private sealed class TrackingSaveChangesPipeline : IApplicationSaveChangesPipeline
    {
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

            return false;
        }

        public ValueTask<bool> ApplyAfterSaveChangesAsync(
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            cancellationToken.ThrowIfCancellationRequested();

            AfterSaveChangesAsyncCallCount++;

            return ValueTask.FromResult(false);
        }
    }
}
