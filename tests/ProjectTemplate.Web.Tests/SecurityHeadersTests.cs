using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for configurable security header middleware behavior.
/// </summary>
public sealed class SecurityHeadersTests
{
    /// <summary>
    /// Verifies that default security headers are applied when security headers are enabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DefaultSecurityHeaders_AreApplied()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "true"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/test/security-headers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "strict-origin-when-cross-origin");
        AssertHeader(response, "X-Permitted-Cross-Domain-Policies", "none");
        AssertHeader(response, "Cross-Origin-Opener-Policy", "same-origin");
        AssertHeader(response, "Cross-Origin-Resource-Policy", "same-origin");
        AssertHeader(response, "Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)");
        AssertHeader(
            response,
            "Content-Security-Policy",
            "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';");

        AssertHeaderMissing(response, "X-XSS-Protection");
    }

    /// <summary>
    /// Verifies that the Content-Security-Policy header is not emitted when disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledContentSecurityPolicy_DoesNotEmitContentSecurityPolicyHeader()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
            ["ProjectTemplate:SecurityHeaders:EnableContentSecurityPolicy"] = "false"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/test/security-headers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeaderMissing(response, "Content-Security-Policy");
    }

    /// <summary>
    /// Verifies that the Permissions-Policy header is not emitted when disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledPermissionsPolicy_DoesNotEmitPermissionsPolicyHeader()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
            ["ProjectTemplate:SecurityHeaders:EnablePermissionsPolicy"] = "false"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/test/security-headers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeaderMissing(response, "Permissions-Policy");
    }

    /// <summary>
    /// Verifies that cross-origin headers are not emitted when disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledCrossOriginHeaders_DoNotEmitCrossOriginHeaders()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
            ["ProjectTemplate:SecurityHeaders:EnableCrossOriginHeaders"] = "false"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/test/security-headers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeaderMissing(response, "Cross-Origin-Opener-Policy");
        AssertHeaderMissing(response, "Cross-Origin-Resource-Policy");
    }

    /// <summary>
    /// Verifies that startup validation fails when an excluded security-header path prefix does not start with '/'.
    /// </summary>
    [Fact]
    public void SecurityHeaders_InvalidExcludedPathPrefix_FailsStartup()
    {
        OptionsValidationException exception =
            AssertSecurityHeadersOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
                    ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:0"] = "health"
                });

        Assert.Contains(
            "ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes values must start with '/'",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when Content-Security-Policy is enabled but no policy value is configured.
    /// </summary>
    [Fact]
    public void SecurityHeaders_CspEnabledWithEmptyPolicy_FailsStartup()
    {
        OptionsValidationException exception =
            AssertSecurityHeadersOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
                    ["ProjectTemplate:SecurityHeaders:EnableContentSecurityPolicy"] = "true",
                    ["ProjectTemplate:SecurityHeaders:ContentSecurityPolicy"] = string.Empty
                });

        Assert.Contains(
            "ProjectTemplate:SecurityHeaders:ContentSecurityPolicy is required when CSP is enabled",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that no security headers are emitted when security headers are globally disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledSecurityHeaders_DoNotEmitSecurityHeaders()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "false"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/test/security-headers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertSecurityHeadersMissing(response);
    }

    /// <summary>
    /// Verifies that configured excluded path prefixes do not receive security headers.
    /// </summary>
    /// <param name="path">The excluded request path to verify.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    [InlineData("/metrics")]
    public async Task ExcludedPathPrefixes_DoNotApplySecurityHeaders(string path)
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:Enabled"] = "true",
            ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:0"] = "/health",
            ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:1"] = "/metrics"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AssertSecurityHeadersMissing(response);
    }

    /// <summary>
    /// Creates a test application factory with the supplied in-memory configuration overrides.
    /// </summary>
    /// <param name="configurationValues">The configuration key/value pairs used to override application settings for a test.</param>
    /// <returns>A configured <see cref="ApplicationWebApplicationFactory"/> instance.</returns>
    private static ApplicationWebApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?> configurationValues)
    {
        return ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess(configurationValues);
    }

    private static void AssertSecurityHeadersMissing(HttpResponseMessage response)
    {
        AssertHeaderMissing(response, "X-Content-Type-Options");
        AssertHeaderMissing(response, "X-Frame-Options");
        AssertHeaderMissing(response, "Referrer-Policy");
        AssertHeaderMissing(response, "X-Permitted-Cross-Domain-Policies");
        AssertHeaderMissing(response, "Cross-Origin-Opener-Policy");
        AssertHeaderMissing(response, "Cross-Origin-Resource-Policy");
        AssertHeaderMissing(response, "Permissions-Policy");
        AssertHeaderMissing(response, "Content-Security-Policy");
        AssertHeaderMissing(response, "X-XSS-Protection");
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out IEnumerable<string>? values), $"Expected response header '{name}'.");

        string actualValue = string.Join(", ", values);

        Assert.Equal(expectedValue, actualValue);
    }

    private static void AssertHeaderMissing(HttpResponseMessage response, string name)
    {
        Assert.False(response.Headers.Contains(name), $"Did not expect response header '{name}'.");
    }

    private static OptionsValidationException AssertSecurityHeadersOptionsValidationFails(
        IReadOnlyDictionary<string, string?> configurationValues)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        IServiceCollection services = new ServiceCollection();

        _ = services.AddApplicationSecurityHeaders(configuration);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        return Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<ApplicationSecurityHeadersOptions>>()
                .Value);
    }

    private static OptionsValidationException? FindOptionsValidationException(Exception exception)
    {
        if (exception is OptionsValidationException optionsValidationException)
        {
            return optionsValidationException;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception innerException in aggregateException.Flatten().InnerExceptions)
            {
                OptionsValidationException? foundException =
                    FindOptionsValidationException(innerException);

                if (foundException is not null)
                {
                    return foundException;
                }
            }
        }

        return exception.InnerException is null
            ? null
            : FindOptionsValidationException(exception.InnerException);
    }
}
