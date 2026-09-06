using System.Net;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

public sealed class RootEndpointTests
{
    [Fact]
    public async Task RootEndpoint_ReturnsLandingPage()
    {
        using var factory = ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Contains(".NET Core Application Template", body, StringComparison.Ordinal);
        Assert.Contains("ASP.NET Core Baseline", body, StringComparison.Ordinal);
        Assert.Contains("Secure defaults", body, StringComparison.Ordinal);
        Assert.Contains("Operational clarity", body, StringComparison.Ordinal);
        Assert.Contains("Template ready", body, StringComparison.Ordinal);
        Assert.Contains("/css/landing.css", body, StringComparison.Ordinal);
    }
}
