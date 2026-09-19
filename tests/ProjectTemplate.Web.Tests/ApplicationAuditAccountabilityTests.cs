using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditAccountabilityTests
{
    [Fact]
    public async Task SaveChangesAsync_SharedContext_GroupsMutationsAndExposesReceipt()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        ApplicationAuditContext auditContext = new(
            actorId: "user-365",
            actorType: ApplicationAuditActorTypes.Human,
            actorDisplayName: "Issue 365 User",
            operationExecutionId: "operation-365",
            executionAttemptId: "attempt-2",
            correlationId: "correlation-365",
            traceId: "0123456789abcdef0123456789abcdef",
            spanId: "0123456789abcdef",
            decisionAuditRecordId: "decision-634",
            tenantHash: "tenant-hash",
            organizationHash: "organization-hash");

        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(CreateDataAccessOptions()),
            auditContextAccessor: new FixedApplicationAuditContextAccessor(auditContext));

        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.ExternalLoginAccounts.AddRangeAsync(
            CreateAccount("github", "user-1"),
            CreateAccount("microsoft", "user-2"));

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<AuditRecord> records = await context.AuditRecords
            .OrderBy(record => record.Entity)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, records.Count);
        Assert.Single(records.Select(record => record.MutationBatchId).Distinct(StringComparer.Ordinal));
        Assert.All(records, record =>
        {
            Assert.Equal("1.0", record.SchemaVersion);
            Assert.Equal("Issue 365 User", record.ModifiedBy);
            Assert.Equal("user-365", record.ActorId);
            Assert.Equal(ApplicationAuditActorTypes.Human, record.ActorType);
            Assert.Equal("operation-365", record.OperationExecutionId);
            Assert.Equal("attempt-2", record.ExecutionAttemptId);
            Assert.Equal("correlation-365", record.CorrelationId);
            Assert.Equal("0123456789abcdef0123456789abcdef", record.TraceId);
            Assert.Equal("0123456789abcdef", record.SpanId);
            Assert.Equal("decision-634", record.DecisionAuditRecordId);
            Assert.Equal("tenant-hash", record.TenantHash);
            Assert.Equal("organization-hash", record.OrganizationHash);
        });

        ApplicationMutationAuditReceipt receipt = Assert.IsType<ApplicationMutationAuditReceipt>(
            pipeline.LastCompletedReceipt);

        Assert.Equal(records[0].MutationBatchId, receipt.MutationBatchId);
        Assert.Equal(2, receipt.AuditRecordCount);
        Assert.Equal("Committed", receipt.PersistenceOutcome);
        Assert.Equal("operation-365", receipt.OperationExecutionId);
        Assert.Equal("attempt-2", receipt.ExecutionAttemptId);
        Assert.Equal("decision-634", receipt.DecisionAuditRecordId);
        Assert.Equal("correlation-365", receipt.CorrelationId);
    }

    [Fact]
    public async Task SaveChangesAsync_CustomValuePolicy_ProtectsValuesBeforePersistence()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(CreateDataAccessOptions()),
            auditValuePolicy: new TestAuditValuePolicy());

        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = CreateAccount("LongProviderName", "sensitive-provider-id");
        account.DisplayName = "Sensitive Display Name";
        account.Email = "sensitive@example.com";

        _ = await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AuditRecord record = await context.AuditRecords.SingleAsync(
            TestContext.Current.CancellationToken);

        using var values = JsonDocument.Parse(record.CurrentValues);

        Assert.Equal("***", values.RootElement.GetProperty(nameof(ExternalLoginAccount.Email)).GetString());
        Assert.False(values.RootElement.TryGetProperty(nameof(ExternalLoginAccount.DisplayName), out _));

        string providerIdHash = values.RootElement
            .GetProperty(nameof(ExternalLoginAccount.ProviderUserId))
            .GetString()!;

        Assert.Equal(64, providerIdHash.Length);
        Assert.DoesNotContain("sensitive-provider-id", record.CurrentValues, StringComparison.Ordinal);
        Assert.Equal("Long", values.RootElement.GetProperty(nameof(ExternalLoginAccount.ProviderName)).GetString());
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        ApplicationSaveChangesPipeline saveChangesPipeline)
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

    private static DataAccessOptions CreateDataAccessOptions()
    {
        return new DataAccessOptions
        {
            Auditing = new DataAuditingOptions
            {
                Enabled = true,
                StorageMode = AuditStorageModes.Local
            }
        };
    }

    private static ExternalLoginAccount CreateAccount(
        string providerName,
        string providerUserId)
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = providerName,
            ProviderUserId = providerUserId,
            DisplayName = "Test User",
            Email = $"{providerUserId}@example.com",
            CreatedOnUtc = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class FixedApplicationAuditContextAccessor(
        ApplicationAuditContext auditContext)
        : IApplicationAuditContextAccessor
    {
        public ApplicationAuditContext Current { get; } = auditContext;
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Fallback Actor";
    }

    private sealed class TestAuditValuePolicy : IApplicationAuditValuePolicy
    {
        public ApplicationAuditValueDecision Evaluate(
            Type entityType,
            string propertyName,
            object? value)
        {
            return propertyName switch
            {
                nameof(ExternalLoginAccount.Email) => new(ApplicationAuditValueDisposition.Mask),
                nameof(ExternalLoginAccount.DisplayName) => new(ApplicationAuditValueDisposition.Omit),
                nameof(ExternalLoginAccount.ProviderUserId) => new(ApplicationAuditValueDisposition.Hash),
                nameof(ExternalLoginAccount.ProviderName) => new(ApplicationAuditValueDisposition.Truncate, 4),
                _ => ApplicationAuditValueDecision.Include
            };
        }
    }
}
