using System.Net;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies the complete HTML document shared by controller-rendered views.
/// </summary>
public sealed class MvcViewLayoutTests
{
    /// <summary>
    /// Verifies that account and browser error views include the application document shell and stylesheet.
    /// </summary>
    /// <param name="path">The controller view endpoint to request.</param>
    /// <param name="expectedStatusCode">The status code expected from the endpoint.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData("/Account/Login", HttpStatusCode.OK)]
    [InlineData("/Account/AccessDenied", HttpStatusCode.Forbidden)]
    [InlineData("/missing-browser-page", HttpStatusCode.NotFound)]
    public async Task ControllerViews_RenderCompleteStyledDocuments(
        string path,
        HttpStatusCode expectedStatusCode)
    {
        using var factory =
            ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html lang=\"en\">", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<meta charset=\"utf-8\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/css/landing.css?v=", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<main class=\"landing-shell\">", content, StringComparison.OrdinalIgnoreCase);
    }
}
