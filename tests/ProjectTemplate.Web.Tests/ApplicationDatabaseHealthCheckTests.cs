using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.HealthChecks;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides tests for the application database readiness check.
/// </summary>
/// <remarks>
/// These tests build the service collection directly instead of using the web application factory. The data access
/// connection string is resolved when services are registered, which is before the factory's test configuration is
/// applied, so a factory-based test cannot point the application at a specific database.
/// </remarks>
public sealed class ApplicationDatabaseHealthCheckTests
{
    /// <summary>
    /// Verifies that readiness is unhealthy when the SQLite database file does not exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Readiness_DatabaseUnavailable_ReportsUnhealthy()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ncat-readiness-missing-{Guid.NewGuid():N}.db");

        HealthReport report = await CheckReadinessAsync(CreateSqliteConfiguration(databasePath));

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(ApplicationDatabaseHealthCheck.UnavailableDescription, entry.Description);
        Assert.False(File.Exists(databasePath));
    }

    /// <summary>
    /// Verifies that readiness is healthy when the SQLite database file exists.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Readiness_DatabaseAvailable_ReportsHealthy()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ncat-readiness-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(databasePath, string.Empty, TestContext.Current.CancellationToken);

        try
        {
            HealthReport report = await CheckReadinessAsync(CreateSqliteConfiguration(databasePath));

            Assert.Equal(HealthStatus.Healthy, report.Status);
            HealthReportEntry entry = Assert.Single(report.Entries).Value;
            Assert.Equal(ApplicationDatabaseHealthCheck.AvailableDescription, entry.Description);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    /// <summary>
    /// Verifies that the check reports healthy without contacting the database when it is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Readiness_CheckDisabled_ReportsHealthyWithoutContactingDatabase()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ncat-readiness-disabled-{Guid.NewGuid():N}.db");

        HealthReport report = await CheckReadinessAsync(
            CreateSqliteConfiguration(databasePath, readinessCheckEnabled: false));

        Assert.Equal(HealthStatus.Healthy, report.Status);
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(ApplicationDatabaseHealthCheck.DisabledDescription, entry.Description);
    }

    /// <summary>
    /// Verifies that no database readiness check is registered when data access is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Readiness_DataAccessDisabled_RegistersNoDatabaseCheck()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectTemplate:DataAccess:Provider"] = "None"
            })
            .Build();

        HealthReport report = await CheckReadinessAsync(configuration);

        Assert.Empty(report.Entries);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    private static async Task<HealthReport> CheckReadinessAsync(IConfiguration configuration)
    {
        ServiceCollection services = new();
        _ = services.AddLogging();

        // Mirrors Program.cs: the health check service is always registered, and data access adds the database check
        // only when a provider is enabled.
        _ = services.AddApplicationHealthChecks();
        _ = services.AddApplicationDataAccess(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();

        return await provider
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(
                registration => registration.Tags.Contains(ApplicationHealthCheckTags.Ready),
                TestContext.Current.CancellationToken);
    }

    private static IConfiguration CreateSqliteConfiguration(string databasePath, bool readinessCheckEnabled = true)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProjectTemplate:DataAccess:Provider"] = "Sqlite",
                ["ProjectTemplate:DataAccess:ConnectionStringName"] = "ApplicationDatabase",
                ["ConnectionStrings:ApplicationDatabase"] = $"Data Source={databasePath};Pooling=False",
                ["ProjectTemplate:HealthChecks:DatabaseReadinessCheckEnabled"] =
                    readinessCheckEnabled ? "true" : "false"
            })
            .Build();
    }
}
