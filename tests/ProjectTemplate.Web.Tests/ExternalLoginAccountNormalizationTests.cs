using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.ExternalLogins;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ExternalLoginAccountNormalizationTests
{
    [Fact]
    public async Task SaveChangesAsync_LookupSensitiveValues_TrimsAndStoresNormalizedColumns()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = " GitHub ",
            ProviderUserId = " provider-user-123 ",
            DisplayName = " Test User ",
            Email = " User@Example.Com ",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal("GitHub", persisted.ProviderName);
        Assert.Equal("GITHUB", persisted.NormalizedProviderName);
        Assert.Equal("provider-user-123", persisted.ProviderUserId);
        Assert.Equal("Test User", persisted.DisplayName);
        Assert.Equal("User@Example.Com", persisted.Email);
        Assert.Equal("USER@EXAMPLE.COM", persisted.NormalizedEmail);
    }

    [Fact]
    public async Task FindByProviderUserIdAsync_ProviderNameCasingAndWhitespace_ReturnsLinkedAccount()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "case-sensitive-user",
            DisplayName = "Case Test",
            Email = "case@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            " github ",
            "case-sensitive-user",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
    }

    [Fact]
    public async Task FindByProviderUserIdAsync_DifferentProviderUserIdCasing_ReturnsNull()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "CaseSensitiveUser",
            DisplayName = "Case Sensitive User",
            Email = "case-sensitive@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            "GitHub",
            "casesensitiveuser",
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveChangesAsync_DecomposedUnicode_NormalizesToFormC()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        string decomposedDisplayName = "Jose\u0301 User";

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "unicode-user",
            DisplayName = decomposedDisplayName,
            Email = "unicode@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal("José User", persisted.DisplayName);
        Assert.True(persisted.DisplayName!.IsNormalized(System.Text.NormalizationForm.FormC));
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
