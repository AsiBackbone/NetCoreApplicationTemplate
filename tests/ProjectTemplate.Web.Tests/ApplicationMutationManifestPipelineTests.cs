using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationMutationManifestPipelineTests
{
    [Fact]
    public async Task SaveChangesAsync_ProtectedAuditRecords_VerifyAgainstReceipt()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var builder = new CanonicalApplicationMutationManifestBuilder();
        var hasher = new Sha256ApplicationMutationManifestHasher();
        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(CreateDataAccessOptions()),
            auditValuePolicy: new TestAuditValuePolicy(),
            manifestBuilder: builder,
            manifestHasher: hasher);

        await using ApplicationDbContext context = CreateContext(connection, pipeline);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        ExternalLoginAccount account = new()
        {
            LocalUserId = Guid.NewGuid(),
            ProviderName = "LongProviderName",
            ProviderUserId = "sensitive-provider-id",
            DisplayName = "Sensitive Display Name",
            Email = "sensitive@example.com",
            CreatedOnUtc = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc)
        };

        _ = await context.ExternalLoginAccounts.AddAsync(account, TestContext.Current.CancellationToken);
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        List<AuditRecord> records = await context.AuditRecords
            .ToListAsync(TestContext.Current.CancellationToken);
        ApplicationMutationAuditReceipt receipt = Assert.IsType<ApplicationMutationAuditReceipt>(
            pipeline.LastCompletedReceipt);
        var verifier = new ApplicationMutationManifestVerifier(builder, hasher);
        ApplicationMutationManifest manifest = builder.Build(records);

        Assert.Equal("SHA-256", receipt.MutationManifestAlgorithm);
        Assert.Equal(ApplicationMutationManifest.CurrentSchemaVersion, receipt.MutationManifestSchemaVersion);
        Assert.Equal(64, receipt.MutationManifestHash.Length);
        Assert.True(verifier.Verify(receipt, records));
        Assert.Contains("***", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("Long", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive@example.com", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive Display Name", manifest.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-provider-id", manifest.CanonicalJson, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext(
        SqliteConnection connection,
        ApplicationSaveChangesPipeline pipeline)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var interceptorForSavePipeline = new ApplicationSaveChangesInterceptor(pipeline);

        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            interceptorForSavePipeline);
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

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Manifest Test Actor";
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
