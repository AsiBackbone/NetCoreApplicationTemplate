using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for API versioning configuration and routing.
/// </summary>
public sealed class ApiVersioningTests
{
    [Fact]
    public async Task UrlSegmentVersionedEndpoint_ReturnsOk()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response =
            await client.GetAsync("/api/v1/application-information", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("api-supported-versions"));
        Assert.True(response.Headers.Contains("api-deprecated-versions"));

        ApplicationInformationResponse? body =
            await response.Content.ReadFromJsonAsync<ApplicationInformationResponse>(
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("ProjectTemplate.Web", body.ApplicationName);
        Assert.Equal("1.0", body.ApiVersion);
    }

    [Fact]
    public async Task HeaderVersionedEndpoint_ReturnsOk()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/application-information");
        request.Headers.Add("X-API-Version", "1.0");

        using HttpResponseMessage response =
            await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeprecatedUrlSegmentVersion_ReturnsDeprecationHeaders()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response =
            await client.GetAsync("/api/v0.9/application-information", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("api-deprecated-versions"));
        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.True(response.Headers.Contains("Link"));

        // No Sunset date is configured by default, so no removal date is advertised. A template cannot supply a
        // meaningful one; see DeprecatedVersionSunsetHeaderTests for the configured case.
        Assert.False(response.Headers.Contains("Sunset"));

        ApplicationInformationResponse? body =
            await response.Content.ReadFromJsonAsync<ApplicationInformationResponse>(
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("0.9", body.ApiVersion);
    }

    [Fact]
    public async Task UnsupportedApiVersion_ReturnsNotFound()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response =
            await client.GetAsync("/api/v2/application-information", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void ApiVersioningOptions_AreBoundFromConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ApiVersioning:DefaultMajorVersion"] = "2",
            ["ProjectTemplate:ApiVersioning:DefaultMinorVersion"] = "1",
            ["ProjectTemplate:ApiVersioning:AssumeDefaultVersionWhenUnspecified"] = "false",
            ["ProjectTemplate:ApiVersioning:ReportApiVersions"] = "false",
            ["ProjectTemplate:ApiVersioning:EnableUrlSegmentVersioning"] = "true",
            ["ProjectTemplate:ApiVersioning:EnableHeaderVersioning"] = "true",
            ["ProjectTemplate:ApiVersioning:HeaderName"] = "X-Test-Api-Version"
        });

        ApplicationApiVersioningOptions options = factory.Services
            .GetRequiredService<IOptions<ApplicationApiVersioningOptions>>()
            .Value;

        Assert.Equal(2, options.DefaultMajorVersion);
        Assert.Equal(1, options.DefaultMinorVersion);
        Assert.False(options.AssumeDefaultVersionWhenUnspecified);
        Assert.False(options.ReportApiVersions);
        Assert.True(options.EnableUrlSegmentVersioning);
        Assert.True(options.EnableHeaderVersioning);
        Assert.Equal("X-Test-Api-Version", options.HeaderName);
    }

    private static ApplicationWebApplicationFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? configurationValues = null)
    {
        return new ApplicationWebApplicationFactory(
            configurationValues ?? new Dictionary<string, string?>());
    }

    private sealed record ApplicationInformationResponse(
        string ApplicationName,
        string ApiVersion,
        string Message);
}
