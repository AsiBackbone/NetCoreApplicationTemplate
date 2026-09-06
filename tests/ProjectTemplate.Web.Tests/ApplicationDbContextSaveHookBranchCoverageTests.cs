using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextSaveHookBranchCoverageTests
{
    [Fact]
    public async Task SaveChangesAsync_AddedExternalLoginAccount_NormalizesStampsAndAudits()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            ConcurrencyStamp = " ",
            LocalUserId = Guid.NewGuid(),
            ProviderName = " Git&amp;Hub ",
            ProviderUserId = " external-user&#39;250 ",
            DisplayName = null,
            Email = "   ",
            CreatedOnUtc = new DateTime(2026, 5, 31, 12, 34, 56, 789, DateTimeKind.Utc)
                .AddTicks(1_234)
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(account.ConcurrencyStamp));
        Assert.NotEqual(" ", account.ConcurrencyStamp);
        Assert.Equal(32, account.ConcurrencyStamp.Length);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Git&Hub", persisted.ProviderName);
        Assert.Equal("GIT&HUB", persisted.NormalizedProviderName);
        Assert.Equal("external-user'250", persisted.ProviderUserId);
        Assert.Null(persisted.DisplayName);
        Assert.Null(persisted.Email);
        Assert.Null(persisted.NormalizedEmail);
        Assert.Equal(0, persisted.CreatedOnUtc.Ticks % TimeSpan.TicksPerMillisecond);

        AuditRecord auditRecord = await context.AuditRecords
            .SingleAsync(item => item.Entity == "ExternalLoginAccounts", TestContext.Current.CancellationToken);

        Assert.Equal("Issue250Actor", auditRecord.ModifiedBy);
        Assert.Equal(EntityState.Added.ToString(), auditRecord.State);
        Assert.Equal(0, auditRecord.ModifiedOnUtc.Ticks % TimeSpan.TicksPerMillisecond);

        using var currentValues = JsonDocument.Parse(auditRecord.CurrentValues);

        Assert.Equal("Git&Hub", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.ProviderName)).GetString());
        Assert.Equal("GIT&HUB", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.NormalizedProviderName)).GetString());
        Assert.Equal("external-user'250", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.ProviderUserId)).GetString());
        Assert.Equal("***", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.DisplayName)).GetString());
        Assert.Equal("***", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.Email)).GetString());
        Assert.Equal(account.ConcurrencyStamp, currentValues.RootElement.GetProperty(nameof(DataEntity.ConcurrencyStamp)).GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedExternalLoginAccount_AuditsOnlyModifiedValuesAndUpdatesStamp()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "GitHub",
            providerUserId: "modified-user",
            displayName: "Original User",
            email: "original@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;

        account.DisplayName = " Modified &amp; User ";

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(account.ConcurrencyStamp));
        Assert.NotEqual(originalStamp, account.ConcurrencyStamp);

        context.ChangeTracker.Clear();

        AuditRecord auditRecord = await context.AuditRecords
            .SingleAsync(item => item.Entity == "ExternalLoginAccounts", TestContext.Current.CancellationToken);

        Assert.Equal("Issue250Actor", auditRecord.ModifiedBy);
        Assert.Equal(EntityState.Modified.ToString(), auditRecord.State);

        using var originalValues = JsonDocument.Parse(auditRecord.OriginalValues);
        using var currentValues = JsonDocument.Parse(auditRecord.CurrentValues);

        Assert.Equal("***", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.DisplayName)).GetString());
        Assert.Equal("***", currentValues.RootElement.GetProperty(nameof(ExternalLoginAccount.DisplayName)).GetString());

        Assert.Equal(originalStamp, originalValues.RootElement.GetProperty(nameof(DataEntity.ConcurrencyStamp)).GetString());
        Assert.Equal(account.ConcurrencyStamp, currentValues.RootElement.GetProperty(nameof(DataEntity.ConcurrencyStamp)).GetString());

        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.ProviderName), out _));
        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.ProviderUserId), out _));
        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.Email), out _));
        Assert.False(currentValues.RootElement.TryGetProperty(nameof(ExternalLoginAccount.NormalizedEmail), out _));
    }

    [Fact]
    public async Task SaveChanges_UnchangedEntity_DoesNotCreateAuditRecordOrChangeStamp()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "Microsoft",
            providerUserId: "unchanged-sync-user",
            displayName: "Unchanged Sync User",
            email: "unchanged.sync@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;

        int result = context.SaveChanges();

        Assert.Equal(0, result);
        Assert.Equal(originalStamp, account.ConcurrencyStamp);

        int auditRecordCount = await context.AuditRecords
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, auditRecordCount);
    }

    [Fact]
    public async Task SaveChangesAsync_UnchangedEntity_DoesNotCreateAuditRecordOrChangeStamp()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "Microsoft",
            providerUserId: "unchanged-user",
            displayName: "Unchanged User",
            email: "unchanged@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        string originalStamp = account.ConcurrencyStamp;

        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Equal(originalStamp, account.ConcurrencyStamp);

        int auditRecordCount = await context.AuditRecords
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, auditRecordCount);
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntity_AuditsOriginalValues()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        Guid accountId = await SeedExternalLoginAccountAsync(
            connection,
            providerName: "Google",
            providerUserId: "deleted-user",
            displayName: "Deleted User",
            email: "deleted@example.com");

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);

        ExternalLoginAccount account = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == accountId, TestContext.Current.CancellationToken);

        _ = context.ExternalLoginAccounts.Remove(account);

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        Assert.False(await context.ExternalLoginAccounts
            .AnyAsync(item => item.Id == accountId, TestContext.Current.CancellationToken));

        AuditRecord auditRecord = await context.AuditRecords
            .SingleAsync(item => item.Entity == "ExternalLoginAccounts", TestContext.Current.CancellationToken);

        Assert.Equal("Issue250Actor", auditRecord.ModifiedBy);
        Assert.Equal(EntityState.Deleted.ToString(), auditRecord.State);
        Assert.Equal(string.Empty, auditRecord.CurrentValues);

        using var originalValues = JsonDocument.Parse(auditRecord.OriginalValues);

        Assert.Equal("Google", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.ProviderName)).GetString());
        Assert.Equal("deleted-user", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.ProviderUserId)).GetString());
        Assert.Equal("***", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.DisplayName)).GetString());
        Assert.Equal("***", originalValues.RootElement.GetProperty(nameof(ExternalLoginAccount.Email)).GetString());
    }

    [Fact]
    public async Task SaveChangesAsync_AuditRecordEntity_IsNotReAuditedOrCanonicalized()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        AuditRecord auditRecord = new()
        {
            ConcurrencyStamp = " ",
            ModifiedBy = "Manual&amp;Actor",
            ModifiedOnUtc = new DateTime(2026, 5, 31, 10, 11, 12, 345, DateTimeKind.Utc).AddTicks(6_789),
            Application = "Manual&amp;App",
            Entity = "ExternalLoginAccounts&amp;Manual",
            State = "Added&amp;Manual",
            KeyValues = /*lang=json,strict*/ "{\"Id\":\"abc&amp;123\"}",
            OriginalValues = string.Empty,
            CurrentValues = /*lang=json,strict*/ "{\"DisplayName\":\"O&amp;#39;Connor &amp;amp; Sons\"}"
        };

        await context.AuditRecords.AddAsync(auditRecord, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        List<AuditRecord> auditRecords = await context.AuditRecords
            .ToListAsync(TestContext.Current.CancellationToken);

        AuditRecord persisted = Assert.Single(auditRecords);

        Assert.Equal("Manual&amp;Actor", persisted.ModifiedBy);
        Assert.Equal("Manual&amp;App", persisted.Application);
        Assert.Equal("ExternalLoginAccounts&amp;Manual", persisted.Entity);
        Assert.Equal("Added&amp;Manual", persisted.State);
        Assert.Equal(/*lang=json,strict*/ "{\"Id\":\"abc&amp;123\"}", persisted.KeyValues);
        Assert.Equal(/*lang=json,strict*/ "{\"DisplayName\":\"O&amp;#39;Connor &amp;amp; Sons\"}", persisted.CurrentValues);
        Assert.DoesNotContain("O'Connor", persisted.CurrentValues, StringComparison.Ordinal);
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

        _ = await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
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

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Issue250Actor";
    }
}
