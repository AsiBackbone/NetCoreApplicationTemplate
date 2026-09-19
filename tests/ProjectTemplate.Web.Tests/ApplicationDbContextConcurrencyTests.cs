using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextConcurrencyTests
{
    [Fact]
    public async Task SaveChangesAsync_StaleEntityUpdate_ThrowsDbUpdateConcurrencyException()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId;

        await using (ApplicationDbContext setupContext = CreateContext(connection, auditingEnabled: false))
        {
            await setupContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            ExternalLoginAccount account = new()
            {
                LocalUserId = Guid.NewGuid(),
                ProviderName = "GitHub",
                ProviderUserId = "concurrency-user",
                DisplayName = "Original User",
                Email = "original@example.com",
                CreatedOnUtc = DateTime.UtcNow
            };

            await setupContext.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
            await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            accountId = account.Id;
        }

        await using ApplicationDbContext staleContext = CreateContext(connection, auditingEnabled: false);
        await using ApplicationDbContext winningContext = CreateContext(connection, auditingEnabled: false);

        ExternalLoginAccount staleAccount = await staleContext.ExternalLoginAccounts
            .SingleAsync(account => account.Id == accountId, TestContext.Current.CancellationToken);

        ExternalLoginAccount winningAccount = await winningContext.ExternalLoginAccounts
            .SingleAsync(account => account.Id == accountId, TestContext.Current.CancellationToken);

        winningAccount.DisplayName = "Winning Update";
        await winningContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        staleAccount.DisplayName = "Stale Update";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () => await staleContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SaveChanges_StaleEntityUpdate_ThrowsDbUpdateConcurrencyException()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();

        Guid accountId;

        using (ApplicationDbContext setupContext = CreateContext(connection, auditingEnabled: false))
        {
            setupContext.Database.EnsureCreated();

            ExternalLoginAccount account = new()
            {
                LocalUserId = Guid.NewGuid(),
                ProviderName = "Microsoft",
                ProviderUserId = "sync-concurrency-user",
                DisplayName = "Original User",
                Email = "original@example.com",
                CreatedOnUtc = DateTime.UtcNow
            };

            setupContext.ExternalLoginAccounts.Add(account);
            setupContext.SaveChanges();

            accountId = account.Id;
        }

        using ApplicationDbContext staleContext = CreateContext(connection, auditingEnabled: false);
        using ApplicationDbContext winningContext = CreateContext(connection, auditingEnabled: false);

        ExternalLoginAccount staleAccount = staleContext.ExternalLoginAccounts
            .Single(account => account.Id == accountId);

        ExternalLoginAccount winningAccount = winningContext.ExternalLoginAccounts
            .Single(account => account.Id == accountId);

        winningAccount.DisplayName = "Winning Sync Update";
        winningContext.SaveChanges();

        staleAccount.DisplayName = "Stale Sync Update";

        Assert.Throws<DbUpdateConcurrencyException>(() => staleContext.SaveChanges());
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_UpdatesConcurrencyStamp()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Google",
            ProviderUserId = "stamp-user",
            DisplayName = "Original User",
            Email = "original@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;

        account.DisplayName = "Updated User";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(account.ConcurrencyStamp));
        Assert.NotEqual(originalStamp, account.ConcurrencyStamp);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");

        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        bool auditingEnabled = true)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        DataAccessOptions dataAccessOptions = new()
        {
            Auditing = new DataAuditingOptions
            {
                Enabled = auditingEnabled
            }
        };

        IApplicationSaveChangesPipeline saveChangesPipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(dataAccessOptions));

        var interceptorForSavePipeline = new ApplicationSaveChangesInterceptor(saveChangesPipeline);

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            interceptorForSavePipeline);
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "UnitTest";
    }
}
