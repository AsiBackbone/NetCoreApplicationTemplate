using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the missing-provider and default-return-URL branches of the external challenge endpoint.
/// </summary>
public sealed class ExternalControllerBranchCoverageTests
{
    private const string _testExternalScheme = "TestExternal";
    private const string _testExternalDisplayName = "Test External";

    [Theory]
    [InlineData("/External/Challenge?returnUrl=%2F")]
    [InlineData("/External/Challenge?provider=%20&returnUrl=%2F")]
    public async Task Challenge_MissingOrBlankProvider_ReturnsBadRequest(string requestUri)
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            requestUri,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Challenge_WithoutReturnUrl_RedirectsToApplicationRoot()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/External/Challenge?provider={_testExternalScheme}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/", response.Headers.Location.OriginalString);
    }

    private static WebApplicationFactory<Program> CreateFactoryWithTestExternalProvider()
    {
        return new ApplicationWebApplicationFactory(new Dictionary<string, string?>())
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services
                .AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestExternalChallengeAuthenticationHandler>(
                    _testExternalScheme,
                    _testExternalDisplayName,
                    _ => { })));
    }
}
