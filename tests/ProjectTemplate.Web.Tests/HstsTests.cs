using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies HTTP Strict Transport Security registration.
/// </summary>
public sealed class HstsTests
{
    // UseHsts() never emits the header for localhost, so requests use a non-loopback host name.
    private static readonly Uri _publicHttpsBaseAddress = new("https://app.example.test");

    /// <summary>
    /// Verifies that the application pipeline emits Strict-Transport-Security outside Development.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ApplicationPipeline_NonDevelopmentHttpsRequest_EmitsStrictTransportSecurity()
    {
        using var factory =
            ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = _publicHttpsBaseAddress,
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync(
            "/test/security-headers",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith(
            "max-age=",
            Assert.Single(response.Headers.GetValues("Strict-Transport-Security")),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that <see cref="SecurityHeadersExtensions.UseApplicationHsts"/> emits the header outside Development.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UseApplicationHsts_Production_EmitsStrictTransportSecurity()
    {
        using HttpResponseMessage response = await SendThroughHstsAsync(Environments.Production);

        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    /// <summary>
    /// Verifies that <see cref="SecurityHeadersExtensions.UseApplicationHsts"/> does not emit the header in Development.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UseApplicationHsts_Development_DoesNotEmitStrictTransportSecurity()
    {
        using HttpResponseMessage response = await SendThroughHstsAsync(Environments.Development);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    private static async Task<HttpResponseMessage> SendThroughHstsAsync(string environmentName)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });
        _ = builder.WebHost.UseTestServer();

        await using WebApplication app = builder.Build();
        _ = app.UseApplicationHsts();
        _ = app.MapGet("/", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = app.GetTestClient();
        client.BaseAddress = _publicHttpsBaseAddress;

        HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        await app.StopAsync(TestContext.Current.CancellationToken);
        return response;
    }
}
