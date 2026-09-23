using System.Net;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for baseline health check endpoints.
/// </summary>
public sealed class HealthCheckTests
{
    /// <summary>
    /// Verifies that the baseline health endpoint returns a healthy response.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that the readiness health endpoint returns a healthy response.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthReadyEndpoint_ReturnsHealthy()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Verifies that the liveness health endpoint returns a healthy response.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthLiveEndpoint_ReturnsHealthy()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Verifies that health endpoints receive only the X-Content-Type-Options security header.
    /// </summary>
    /// <param name="path">The health check path to test.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task HealthEndpoints_ApplyOnlyNoSniffSecurityHeader(string path)
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.False(response.Headers.Contains("Referrer-Policy"));
        Assert.False(response.Headers.Contains("X-Permitted-Cross-Domain-Policies"));
        Assert.False(response.Headers.Contains("Cross-Origin-Opener-Policy"));
        Assert.False(response.Headers.Contains("Cross-Origin-Resource-Policy"));
        Assert.False(response.Headers.Contains("Permissions-Policy"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    private static ApplicationWebApplicationFactory CreateFactory()
    {
        return new ApplicationWebApplicationFactory(new Dictionary<string, string?>());
    }

    /// <summary>
    /// Verifies that the audit health endpoint is mapped and reports healthy when no audit checks are registered.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthAuditEndpoint_WithoutAuditChecks_ReturnsHealthy()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health/audit", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", body);
    }

    /// <summary>
    /// Verifies that the audit health endpoint falls under the <c>/health</c> security header exclusion, which keeps
    /// only <c>X-Content-Type-Options</c>.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthAuditEndpoint_AppliesOnlyExcludedPathSecurityHeaders()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health/audit", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Permissions-Policy"));
    }

    /// <summary>
    /// Verifies that readiness fails when the database readiness check is enabled and the database is unreachable.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthReadyEndpoint_DatabaseUnavailable_ReturnsServiceUnavailable()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ncat-readiness-missing-{Guid.NewGuid():N}.db");

        using ApplicationWebApplicationFactory factory = CreateDatabaseReadinessFactory(databasePath);
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Unhealthy", body);
        Assert.False(File.Exists(databasePath));
    }

    /// <summary>
    /// Verifies that readiness succeeds when the database readiness check is enabled and the database is reachable.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HealthReadyEndpoint_DatabaseAvailable_ReturnsHealthy()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ncat-readiness-{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(databasePath, string.Empty, TestContext.Current.CancellationToken);

        try
        {
            using ApplicationWebApplicationFactory factory = CreateDatabaseReadinessFactory(databasePath);
            using HttpClient client = factory.CreateHttpsClient();

            using HttpResponseMessage response = await client.GetAsync(
                "/health/ready",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Healthy", body);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    private static ApplicationWebApplicationFactory CreateDatabaseReadinessFactory(string databasePath)
    {
        return new ApplicationWebApplicationFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:HealthChecks:DatabaseReadinessCheckEnabled"] = "true",
            ["ProjectTemplate:DataAccess:Provider"] = "Sqlite",
            ["ProjectTemplate:DataAccess:ConnectionStringName"] = "ApplicationDatabase",
            ["ConnectionStrings:ApplicationDatabase"] = $"Data Source={databasePath};Pooling=False"
        });
    }
}
