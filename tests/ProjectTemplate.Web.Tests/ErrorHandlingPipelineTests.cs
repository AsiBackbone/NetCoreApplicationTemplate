using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for the unhandled-exception path through the centralized error-handling pipeline.
/// </summary>
/// <remarks>
/// Error handling is registered once, by <c>UseProblemDetails()</c>. These tests exercise an unhandled exception end
/// to end through the pipeline, so a second environment-aware error-handling registration added alongside it would
/// have to keep these behaviors intact. Status-code responses are covered separately by
/// <see cref="AdvertisedBehaviorTests" />; this file covers the thrown-exception path, which was otherwise only
/// tested at the handler level, below the middleware ordering.
/// </remarks>
public sealed class ErrorHandlingPipelineTests
{
    /// <summary>
    /// Verifies that an unhandled exception on an API-shaped request produces a Problem Details response.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnhandledException_OnApiShapedRequest_ReturnsProblemDetails()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/test/advertised-behavior/problem-details");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
    }

    /// <summary>
    /// Verifies that an unhandled exception on a browser-shaped request produces the re-executed error page.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnhandledException_OnBrowserShapedRequest_ReturnsErrorPage()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/test/advertised-behavior/problem-details");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies that an unhandled exception does not leak exception detail outside Development.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnhandledException_DoesNotLeakExceptionDetail()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/test/advertised-behavior/problem-details");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("Invalid advertised behavior test request.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException", body, StringComparison.Ordinal);
    }
}
