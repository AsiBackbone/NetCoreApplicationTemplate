using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationDbContextTimestampPersistenceTests
{
    [Fact]
    public async Task SaveChangesAsync_UtcTimestampProperties_NormalizesToMillisecondPrecision()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: false);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        DateTime createdOnUtc = new(2026, 5, 30, 12, 34, 56, 789, DateTimeKind.Utc);
        createdOnUtc = createdOnUtc.AddTicks(1_234);

        DateTime updatedOnUtc = new(2026, 5, 30, 13, 14, 15, 456, DateTimeKind.Utc);
        updatedOnUtc = updatedOnUtc.AddTicks(5_678);

        DateTime lastLoginOnUtc = new(2026, 5, 30, 14, 15, 16, 123, DateTimeKind.Utc);
        lastLoginOnUtc = lastLoginOnUtc.AddTicks(9_999);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "GitHub",
            ProviderUserId = "timestamp-user",
            DisplayName = "Timestamp User",
            Email = "timestamp@example.com",
            CreatedOnUtc = createdOnUtc,
            UpdatedOnUtc = updatedOnUtc,
            LastLoginOnUtc = lastLoginOnUtc
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        ExternalLoginAccount persisted = await context.ExternalLoginAccounts
            .SingleAsync(item => item.Id == account.Id, TestContext.Current.CancellationToken);

        Assert.Equal(0, persisted.CreatedOnUtc.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.NotNull(persisted.UpdatedOnUtc);
        Assert.NotNull(persisted.LastLoginOnUtc);
        Assert.Equal(0, persisted.UpdatedOnUtc.Value.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal(0, persisted.LastLoginOnUtc.Value.Ticks % TimeSpan.TicksPerMillisecond);

        Assert.Equal(TruncateToMillisecond(createdOnUtc).Ticks, persisted.CreatedOnUtc.Ticks);
        Assert.Equal(TruncateToMillisecond(updatedOnUtc).Ticks, persisted.UpdatedOnUtc.Value.Ticks);
        Assert.Equal(TruncateToMillisecond(lastLoginOnUtc).Ticks, persisted.LastLoginOnUtc.Value.Ticks);
    }

    [Fact]
    public async Task SaveChangesAsync_AuditingEnabled_AuditTimestampUsesMillisecondPrecision()
    {
        await using SqliteConnection connection = await CreateOpenConnectionAsync();

        await using ApplicationDbContext context = CreateContext(connection, auditingEnabled: true);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "Microsoft",
            ProviderUserId = "audit-timestamp-user",
            DisplayName = "Audit Timestamp User",
            Email = "audit-timestamp@example.com",
            CreatedOnUtc = DateTime.UtcNow.AddTicks(4_321)
        };

        await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        AuditRecord auditRecord = await context.AuditRecords
            .SingleAsync(
                item => item.Entity == "ExternalLoginAccounts",
                TestContext.Current.CancellationToken);

        Assert.Equal(0, auditRecord.ModifiedOnUtc.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal("UnitTest", auditRecord.ModifiedBy);
        Assert.Equal(EntityState.Added.ToString(), auditRecord.State);
    }

    private static DateTime TruncateToMillisecond(DateTime value)
    {
        long normalizedTicks = value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond);

        return new DateTime(normalizedTicks, value.Kind);
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
