using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextBranchGapTests
{
    [Fact]
    public async Task SaveChanges_AuditingDisabled_PersistsCanonicalizedAccountWithoutAuditRecord()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        _ = context.Database.EnsureCreated();

        DateTime localCreatedOnUtc = new(2026, 5, 31, 8, 9, 10, 456, DateTimeKind.Local);
        localCreatedOnUtc = localCreatedOnUtc.AddTicks(1_234);

        DateTime unspecifiedUpdatedOnUtc = new(2026, 5, 31, 9, 10, 11, 987, DateTimeKind.Unspecified);
        unspecifiedUpdatedOnUtc = unspecifiedUpdatedOnUtc.AddTicks(5_678);

        ExternalLoginAccount account = new()
        {
            ConcurrencyStamp = " ",
            LocalUserId = Guid.NewGuid(),
            ProviderName = " Git&amp;Hub ",
            ProviderUserId = " external-user&amp;#39;sync ",
            DisplayName = " Sync &amp;amp; User ",
            Email = " Sync.User&amp;#64;Example.com ",
            CreatedOnUtc = localCreatedOnUtc,
            UpdatedOnUtc = unspecifiedUpdatedOnUtc
        };

        context.ExternalLoginAccounts.Add(account);

        int result = context.SaveChanges();

        Assert.Equal(1, result);
        Assert.False(string.IsNullOrWhiteSpace(account.ConcurrencyStamp));
        Assert.Equal(32, account.ConcurrencyStamp.Length);
        Assert.Equal("Git&Hub", account.ProviderName);
        Assert.Equal("GIT&HUB", account.NormalizedProviderName);
        Assert.Equal("external-user'sync", account.ProviderUserId);
        Assert.Equal("Sync & User", account.DisplayName);
        Assert.Equal("Sync.User@Example.com", account.Email);
        Assert.Equal("SYNC.USER@EXAMPLE.COM", account.NormalizedEmail);
        Assert.Equal(NormalizeExpectedUtc(localCreatedOnUtc), account.CreatedOnUtc);
        Assert.NotNull(account.UpdatedOnUtc);
        Assert.Equal(NormalizeExpectedUtc(unspecifiedUpdatedOnUtc), account.UpdatedOnUtc.Value);

        int auditRecordCount = await context.AuditRecords
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, auditRecordCount);
    }

    [Fact]
    public async Task SaveChanges_ModifiedEmailWithAuditingEnabled_AuditsOnlyChangedEmailValues()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "GitHub",
            providerUserId: "email-branch-user",
            displayName: "Email Branch User",
            email: "original@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;

        account.Email = " New&amp;User@example.com ";

        int result = context.SaveChanges();

        Assert.True(result >= 1);
        Assert.NotEqual(originalStamp, account.ConcurrencyStamp);
        Assert.Equal("New&User@example.com", account.Email);
        Assert.Equal("NEW&USER@EXAMPLE.COM", account.NormalizedEmail);

        AuditRecord auditRecord = await context.AuditRecords
            .SingleAsync(item => item.Entity == "ExternalLoginAccounts", TestContext.Current.CancellationToken);

        Assert.Equal("BranchGapActor", auditRecord.ModifiedBy);
        Assert.Equal(EntityState.Modified.ToString(), auditRecord.State);

        using var originalValues = JsonDocument.Parse(auditRecord.OriginalValues);
        using var currentValues = JsonDocument.Parse(auditRecord.CurrentValues);

        Assert.Equal("***", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.Email)).GetString());
        Assert.Equal("***", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.Email)).GetString());
        Assert.Equal("***", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.NormalizedEmail)).GetString());
        Assert.Equal("***", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.NormalizedEmail)).GetString());
        Assert.Equal(originalStamp, originalValues.RootElement.GetProperty(nameof(DataEntity.ConcurrencyStamp)).GetString());
        Assert.Equal(account.ConcurrencyStamp, currentValues.RootElement.GetProperty(nameof(DataEntity.ConcurrencyStamp)).GetString());

        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.ProviderName), out _));
        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.ProviderUserId), out _));
        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.DisplayName), out _));
    }

    [Fact]
    public async Task SaveChangesAsync_AuditingDisabled_ModifiedAccountUpdatesStampWithoutAuditRecord()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "Microsoft",
            providerUserId: "async-disabled-user",
            displayName: "Original Async User",
            email: "async@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;
        account.DisplayName = " Updated &amp; Async User ";

        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result);
        Assert.Equal("Updated & Async User", account.DisplayName);
        Assert.NotEqual(originalStamp, account.ConcurrencyStamp);

        int auditRecordCount = await context.AuditRecords
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, auditRecordCount);
    }

    [Fact]
    public async Task SaveChangesAsync_LocalAndUnspecifiedUtcTimestamps_NormalizesToUtcBeforePersisting()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        DateTime localCreatedOnUtc = new(2026, 5, 31, 6, 7, 8, 123, DateTimeKind.Local);
        localCreatedOnUtc = localCreatedOnUtc.AddTicks(4_567);

        DateTime unspecifiedUpdatedOnUtc = new(2026, 5, 31, 7, 8, 9, 234, DateTimeKind.Unspecified);
        unspecifiedUpdatedOnUtc = unspecifiedUpdatedOnUtc.AddTicks(5_678);

        DateTime localLastLoginOnUtc = new(2026, 5, 31, 8, 9, 10, 345, DateTimeKind.Local);
        localLastLoginOnUtc = localLastLoginOnUtc.AddTicks(6_789);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Google",
            ProviderUserId = "timestamp-kind-user",
            DisplayName = "Timestamp Kind User",
            Email = "timestamp-kind@example.com",
            CreatedOnUtc = localCreatedOnUtc,
            UpdatedOnUtc = unspecifiedUpdatedOnUtc,
            LastLoginOnUtc = localLastLoginOnUtc
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(account.UpdatedOnUtc);
        Assert.NotNull(account.LastLoginOnUtc);
        Assert.Equal(NormalizeExpectedUtc(localCreatedOnUtc), account.CreatedOnUtc);
        Assert.Equal(NormalizeExpectedUtc(unspecifiedUpdatedOnUtc), account.UpdatedOnUtc.Value);
        Assert.Equal(NormalizeExpectedUtc(localLastLoginOnUtc), account.LastLoginOnUtc.Value);
        Assert.Equal(DateTimeKind.Utc, account.CreatedOnUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, account.UpdatedOnUtc.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, account.LastLoginOnUtc.Value.Kind);
    }

    private static async Task<Guid> SeedExternalLoginAccountAsync(
        SqliteConnection connection,
        string providerName,
        string providerUserId,
        string displayName,
        string email)
    {
        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = providerName,
            ProviderUserId = providerUserId,
            DisplayName = displayName,
            Email = email,
            CreatedOnUtc = new DateTime(2026, 5, 31, 8, 0, 0, DateTimeKind.Utc)
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return account.Id;
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");

        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        bool auditingEnabled)
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

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            saveChangesPipeline);
    }

    private static DateTime NormalizeExpectedUtc(DateTime value)
    {
        DateTime utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        long normalizedTicks = utcValue.Ticks - (utcValue.Ticks % TimeSpan.TicksPerMillisecond);

        return new DateTime(normalizedTicks, DateTimeKind.Utc);
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "BranchGapActor";
    }
}
