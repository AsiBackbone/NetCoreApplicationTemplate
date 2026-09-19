using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditStoreSeamTests
{
    [Fact]
    public async Task SaveChangesAsync_CustomAuditStore_ReceivesAuditRecordWithoutLocalAuditStorage()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var auditStore = new CapturingApplicationAuditStore();

        await using ApplicationDbContext context = CreateContext(connection, auditStore);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = " GitHub ",
            ProviderUserId = " custom-store-user ",
            DisplayName = "Custom Store User",
            Email = "custom.store@example.com",
            CreatedOnUtc = new DateTime(2026, 6, 27, 8, 0, 0, DateTimeKind.Utc)
        };

        _ = await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AuditRecord capturedRecord = Assert.Single(auditStore.Records);

        Assert.Equal(1, auditStore.AsyncAppendCount);
        Assert.Equal(0, auditStore.SyncAppendCount);
        Assert.Equal("ExternalLoginAccounts", capturedRecord.Entity);
        Assert.Equal(EntityState.Added.ToString(), capturedRecord.State);
        Assert.Equal("AuditSeamActor", capturedRecord.ModifiedBy);

        int localAuditRecordCount = await context.AuditRecords
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, localAuditRecordCount);
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        IApplicationAuditStore auditStore)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        DataAccessOptions dataAccessOptions = new()
        {
            Auditing = new DataAuditingOptions
            {
                Enabled = true,
                StorageMode = AuditStorageModes.ExternalSink
            }
        };

        IApplicationSaveChangesPipeline saveChangesPipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(dataAccessOptions),
            auditStore);

        var interceptorForSavePipeline = new ApplicationSaveChangesInterceptor(saveChangesPipeline);

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            interceptorForSavePipeline);
    }

    private sealed class CapturingApplicationAuditStore : IApplicationAuditStore
    {
        private readonly List<AuditRecord> _records = [];

        public IReadOnlyList<AuditRecord> Records => _records;

        public int SyncAppendCount { get; private set; }

        public int AsyncAppendCount { get; private set; }

        public void Append(
            ApplicationDbContext dbContext,
            AuditRecord auditRecord)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(auditRecord);

            SyncAppendCount++;
            _records.Add(auditRecord);
        }

        public ValueTask AppendAsync(
            ApplicationDbContext dbContext,
            AuditRecord auditRecord,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(auditRecord);
            cancellationToken.ThrowIfCancellationRequested();

            AsyncAppendCount++;
            _records.Add(auditRecord);

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "AuditSeamActor";
    }
}
