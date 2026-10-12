using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.HealthChecks;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the in-memory and blank SQLite data-source branches of <c>ApplicationDatabaseHealthCheck</c>, including a
/// connection probe that fails after the missing-file shortcut is skipped.
/// </summary>
/// <remarks>
/// Like <c>ApplicationDatabaseHealthCheckTests</c>, these tests build the service collection directly because the data
/// access connection string is resolved when services are registered.
/// </remarks>
public sealed class ApplicationDatabaseHealthCheckBranchCoverageTests
{
    [Fact]
    public async Task Readiness_InMemorySqliteDatabase_ReportsHealthy()
    {
        HealthReport report = await CheckReadinessAsync("Data Source=:memory:;Pooling=False");

        Assert.Equal(HealthStatus.Healthy, report.Status);
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(ApplicationDatabaseHealthCheck.AvailableDescription, entry.Description);
    }

    [Fact]
    public async Task Readiness_WhitespaceSqliteDataSource_ReportsUnhealthyAfterConnectionProbe()
    {
        HealthReport report = await CheckReadinessAsync("Data Source=' ';Mode=ReadOnly;Pooling=False");

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(ApplicationDatabaseHealthCheck.UnavailableDescription, entry.Description);
    }

    private static async Task<HealthReport> CheckReadinessAsync(string connectionString)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectTemplate:DataAccess:Provider"] = "Sqlite",
                ["ProjectTemplate:DataAccess:ConnectionStringName"] = "ApplicationDatabase",
                ["ConnectionStrings:ApplicationDatabase"] = connectionString,
                ["ProjectTemplate:HealthChecks:DatabaseReadinessCheckEnabled"] = "true"
            })
            .Build();
        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddApplicationHealthChecks();
        _ = services.AddApplicationDataAccess(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();

        return await provider
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(
                registration => registration.Tags.Contains(ApplicationHealthCheckTags.Ready),
                TestContext.Current.CancellationToken);
    }
}
