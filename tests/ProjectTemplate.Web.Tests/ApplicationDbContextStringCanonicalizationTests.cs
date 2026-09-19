using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextStringCanonicalizationTests
{
    [Fact]
    public async Task SaveChangesAsync_SingleEncodedString_CanonicalizesBeforePersistence()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "encoded-user",
            DisplayName = "O&#39;Connor &quot;Admin&quot; &amp; Sons",
            Email = "encoded@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal("O'Connor \"Admin\" & Sons", persisted.DisplayName);
    }

    [Fact]
    public async Task SaveChangesAsync_DoubleEncodedString_CanonicalizesBeforePersistence()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Microsoft",
            ProviderUserId = "double-encoded-user",
            DisplayName = "O&amp;#39;Connor &amp;quot;Admin&amp;quot; &amp;amp; Sons",
            Email = "double-encoded@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal("O'Connor \"Admin\" & Sons", persisted.DisplayName);
    }

    [Fact]
    public async Task SaveChangesAsync_RawSpecialCharacters_RemainValid()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        const string displayName = "Élodie O'Connor \"QA\" & Sons";

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Google",
            ProviderUserId = "raw-special-user",
            DisplayName = displayName,
            Email = "raw-special@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal(displayName, persisted.DisplayName);
    }

    [Fact]
    public async Task SaveChangesAsync_SqlLikePayload_PersistsThroughParameterizedAccess()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        const string sqlLikeDisplayName = "Robert'); DROP TABLE ExternalLoginAccounts;--";

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "sql-like-user",
            DisplayName = sqlLikeDisplayName,
            Email = "sql-like@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.ProviderUserId == "sql-like-user", TestContext.Current.CancellationToken);

        int accountCount = await context.ExternalLoginAccounts
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(sqlLikeDisplayName, persisted.DisplayName);
        Assert.Equal(1, accountCount);
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
