using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.ExternalLogins;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides tests for the EF Core external login account-linking resolver.
/// </summary>
public sealed class ExternalLoginAccountResolverTests
{
    /// <summary>
    /// Verifies that an existing provider/user key resolves to the linked external login account.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindByProviderUserIdAsync_ExistingProviderUserId_ReturnsLinkedAccount()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var localUserId = Guid.NewGuid();

        ExternalLoginAccount account = new()
        {
            LocalUserId = localUserId,
            ProviderName = "GitHub",
            ProviderUserId = "github-user-123",
            DisplayName = "Test GitHub User",
            Email = "github-user@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            "GitHub",
            "github-user-123",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
        Assert.Equal(localUserId, result.LocalUserId);
        Assert.Equal("GitHub", result.ProviderName);
        Assert.Equal("github-user-123", result.ProviderUserId);
        Assert.Equal("Test GitHub User", result.DisplayName);
        Assert.Equal("github-user@example.com", result.Email);
    }

    /// <summary>
    /// Verifies that an unknown provider/user key returns null.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindByProviderUserIdAsync_UnknownProviderUserId_ReturnsNull()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            "Google",
            "missing-user",
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that blank provider lookup values return null without querying for an invalid link.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData("", "provider-user-id")]
    [InlineData("   ", "provider-user-id")]
    [InlineData("GitHub", "")]
    [InlineData("GitHub", "   ")]
    public async Task FindByProviderUserIdAsync_BlankLookupValues_ReturnsNull(
        string providerName,
        string providerUserId)
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            providerName,
            providerUserId,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that lookup values are trimmed before provider/user key matching.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindByProviderUserIdAsync_TrimmedLookupValues_ReturnsLinkedAccount()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Microsoft",
            ProviderUserId = "microsoft-user-123",
            DisplayName = "Test Microsoft User",
            Email = "microsoft-user@example.com",
            CreatedOnUtc = DateTime.UtcNow
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        ExternalLoginAccount? result = await resolver.FindByProviderUserIdAsync(
            " Microsoft ",
            " microsoft-user-123 ",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
    }

    /// <summary>
    /// Verifies that all external login links for a local user are returned.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindByLocalUserIdAsync_ExistingLocalUserId_ReturnsLinkedAccounts()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var localUserId = Guid.NewGuid();

        await context.ExternalLoginAccounts.AddRangeAsync(
            [
                new ExternalLoginAccount
                {
                    LocalUserId = localUserId,
                    ProviderName = "GitHub",
                    ProviderUserId = "github-user-123",
                    DisplayName = "GitHub User",
                    Email = "github-user@example.com",
                    CreatedOnUtc = DateTime.UtcNow
                },
                new ExternalLoginAccount
                {
                    LocalUserId = localUserId,
                    ProviderName = "Google",
                    ProviderUserId = "google-user-123",
                    DisplayName = "Google User",
                    Email = "google-user@example.com",
                    CreatedOnUtc = DateTime.UtcNow
                },
                new ExternalLoginAccount
                {
                    LocalUserId = Guid.NewGuid(),
                    ProviderName = "Microsoft",
                    ProviderUserId = "different-user-123",
                    DisplayName = "Different User",
                    Email = "different-user@example.com",
                    CreatedOnUtc = DateTime.UtcNow
                }
            ],
            TestContext.Current.CancellationToken);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        IReadOnlyList<ExternalLoginAccount> results = await resolver.FindByLocalUserIdAsync(
            localUserId,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.All(results, account => Assert.Equal(localUserId, account.LocalUserId));
        Assert.Equal(["GitHub", "Google"], [.. results.Select(account => account.ProviderName)]);
    }

    /// <summary>
    /// Verifies that an empty local user ID returns an empty result set.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindByLocalUserIdAsync_EmptyLocalUserId_ReturnsEmptyResult()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        EfCoreExternalLoginAccountResolver resolver = new(context);

        IReadOnlyList<ExternalLoginAccount> results = await resolver.FindByLocalUserIdAsync(
            Guid.Empty,
            TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that duplicate provider/user keys are rejected by the relational database index.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ExternalLoginAccounts_DuplicateProviderUserId_ThrowsDbUpdateException()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.ExternalLoginAccounts.AddRangeAsync(
            [
                new ExternalLoginAccount
                {
                    LocalUserId = Guid.NewGuid(),
                    ProviderName = "GitHub",
                    ProviderUserId = "duplicate-provider-user",
                    DisplayName = "First User",
                    Email = "first-user@example.com",
                    CreatedOnUtc = DateTime.UtcNow
                },
                new ExternalLoginAccount
                {
                    LocalUserId = Guid.NewGuid(),
                    ProviderName = "GitHub",
                    ProviderUserId = "duplicate-provider-user",
                    DisplayName = "Second User",
                    Email = "second-user@example.com",
                    CreatedOnUtc = DateTime.UtcNow
                }
            ],
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<DbUpdateException>(
            async () => await context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that the default external login persistence model does not include token storage properties.
    /// </summary>
    [Fact]
    public void ExternalLoginAccount_DoesNotExposeProviderTokenStorageProperties()
    {
        string[] disallowedPropertyNames =
        [
            "AccessToken",
            "RefreshToken",
            "IdToken",
            "Token",
            "Tokens",
            "ProviderToken",
            "ProviderTokens"
        ];

        string[] propertyNames = [.. typeof(ExternalLoginAccount)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)];

        Assert.False(
            propertyNames.Any(propertyName => disallowedPropertyNames.Contains(
                propertyName,
                StringComparer.OrdinalIgnoreCase)),
            "ExternalLoginAccount should not store provider tokens by default.");
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
