using System.Net;
using System.Text.Json;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for v1.0 advertised runtime behaviors that consumers rely on.
/// </summary>
public sealed class AdvertisedBehaviorTests
{
    /// <summary>
    /// Verifies that API-style missing endpoints return Problem Details instead of the browser error page.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ApiStyleMissingEndpoint_ReturnsProblemDetailsShape()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/advertised-behavior/missing");

        request.Headers.TryAddWithoutValidation("X-Correlation-ID", "test-correlation-187");

        using HttpResponseMessage response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", root.GetProperty("title").GetString());
        Assert.Equal("/api/v1/advertised-behavior/missing", root.GetProperty("instance").GetString());
        Assert.Equal("test-correlation-187", root.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("requestId").GetString()));
    }

    /// <summary>
    /// Verifies that browser-oriented missing endpoints use the browser error page instead of Problem Details.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task BrowserStyleMissingEndpoint_ReturnsErrorPage()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/missing-browser-page",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Request ID", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("application/problem+json", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that forwarded headers are honored for a proxy-like request from a configured known proxy.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ForwardedHeaders_AreAppliedForConfiguredProxyLikeRequest()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:Enabled"] = "true",
            ["ProjectTemplate:ForwardedHeaders:ClearKnownNetworksAndProxies"] = "true",
            ["ProjectTemplate:ForwardedHeaders:KnownProxies:0"] = "::1"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/advertised-behavior/request-info");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.25");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        JsonElement root = document.RootElement;

        Assert.Equal("https", root.GetProperty("scheme").GetString());
        Assert.Equal("203.0.113.25", root.GetProperty("remoteIpAddress").GetString());
    }

    private static ApplicationWebApplicationFactory CreateFactory()
    {
        return CreateFactory(new Dictionary<string, string?>());
    }

    private static ApplicationWebApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?> configurationValues)
    {
        return ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess(configurationValues);
    }
}
