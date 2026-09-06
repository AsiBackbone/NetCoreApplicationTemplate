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

    /// <summary>
    /// Verifies that a direct request to the error route does not receive the status code it asked for.
    /// </summary>
    /// <param name="requestedStatusCode">The status code supplied in the route.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The error route is anonymous because it serves errors for unauthenticated requests. Honoring its route
    /// value let any caller select the response status, including values outside the range the error page
    /// serves, and produce an error-page log entry per request.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(418)]
    [InlineData(500)]
    [InlineData(999)]
    public async Task DirectRequestToErrorRoute_DoesNotHonorTheRequestedStatusCode(int requestedStatusCode)
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"/Home/Error/{requestedStatusCode}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that a direct request to the error route with no status code is also treated as not found.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DirectRequestToErrorRouteWithoutStatusCode_IsNotFound()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/Home/Error", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies that a genuine missing page still reaches the error page with its real status code.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task GenuineMissingPage_StillReachesTheErrorPageWithItsRealStatusCode()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/no-such-browser-page", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Request ID", body, StringComparison.OrdinalIgnoreCase);
    }
}
