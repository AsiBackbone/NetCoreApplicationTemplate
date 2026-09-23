using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Web.HealthChecks;
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

        using HttpResponseMessage response = await client.GetAsync("/health/audit-integrity", TestContext.Current.CancellationToken);

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

        using HttpResponseMessage response = await client.GetAsync("/health/audit-integrity", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Permissions-Policy"));
    }

    /// <summary>
    /// Verifies that each tagged endpoint runs only checks carrying its tag, so an unhealthy check affects only the
    /// endpoint that selects it.
    /// </summary>
    /// <param name="unhealthyCheckTag">The tag applied to a check that always reports unhealthy.</param>
    /// <param name="readyStatusCode">The expected <c>/health/ready</c> status code.</param>
    /// <param name="auditIntegrityStatusCode">The expected <c>/health/audit-integrity</c> status code.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData(ApplicationHealthCheckTags.Ready, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK)]
    [InlineData(ApplicationHealthCheckTags.Audit, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable)]
    public async Task TaggedHealthEndpoints_RunOnlyChecksWithTheirTag(
        string unhealthyCheckTag,
        HttpStatusCode readyStatusCode,
        HttpStatusCode auditIntegrityStatusCode)
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using WebApplicationFactory<Program> taggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services
                .AddHealthChecks()
                .AddCheck(
                    "test-unhealthy",
                    () => HealthCheckResult.Unhealthy(),
                    tags: [unhealthyCheckTag])));
        using HttpClient client = taggedFactory.CreateHttpsClient();

        using HttpResponseMessage readyResponse = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        using HttpResponseMessage auditIntegrityResponse = await client.GetAsync("/health/audit-integrity", TestContext.Current.CancellationToken);

        Assert.Equal(readyStatusCode, readyResponse.StatusCode);
        Assert.Equal(auditIntegrityStatusCode, auditIntegrityResponse.StatusCode);
    }
}
