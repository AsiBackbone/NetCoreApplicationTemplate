using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the application-name fallback and value serialization branches of the internal <c>AuditEntry</c>.
/// </summary>
/// <remarks>
/// The entry-assembly fallbacks inside <c>ToAuditRecord</c> are not reachable from a test host, because the test
/// process always has an entry assembly with a name.
/// </remarks>
public sealed class AuditEntryBranchCoverageTests
{
    [Fact]
    public void ToAuditRecord_BlankApplicationAndNoValues_UsesAssemblyNameAndEmptyPayloads()
    {
        using ApplicationDbContext context = CreateContext();
        AuditEntry auditEntry = new(context.Entry(CreateAccount()))
        {
            TableName = "ExternalLoginAccounts",
            State = "Added",
            Application = " ",
            MutationBatchId = "audit-entry-blank-application"
        };

        var record = auditEntry.ToAuditRecord();

        Assert.False(string.IsNullOrWhiteSpace(record.Application));
        Assert.Equal("audit-entry-blank-application", record.MutationBatchId);
        Assert.Empty(record.KeyValues);
        Assert.Empty(record.OriginalValues);
        Assert.Empty(record.CurrentValues);
        Assert.False(auditEntry.HasTemporaryProperties);
    }

    [Fact]
    public void ToAuditRecord_ExplicitApplicationAndValues_PreservesApplicationAndSerializesValues()
    {
        using ApplicationDbContext context = CreateContext();
        AuditEntry auditEntry = new(context.Entry(CreateAccount()))
        {
            TableName = "ExternalLoginAccounts",
            State = "Modified",
            Application = "Audit Entry Test Application",
            MutationBatchId = "audit-entry-explicit-application",
            KeyValues = { ["Id"] = 42 },
            CurrentValues = { ["DisplayName"] = "Audit Entry User" }
        };

        var record = auditEntry.ToAuditRecord();

        Assert.Equal("Audit Entry Test Application", record.Application);
        Assert.Equal(/*lang=json,strict*/ "{\"Id\":42}", record.KeyValues);
        Assert.Empty(record.OriginalValues);
        Assert.Contains("Audit Entry User", record.CurrentValues, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions { Enabled = false }
            }));

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            new ApplicationSaveChangesInterceptor(pipeline));
    }

    private static ExternalLoginAccount CreateAccount()
    {
        return new ExternalLoginAccount
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "github",
            ProviderUserId = "audit-entry-user",
            DisplayName = "Audit Entry User",
            Email = "audit-entry-user@example.com",
            CreatedOnUtc = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Audit Entry Test Actor";
    }
}
