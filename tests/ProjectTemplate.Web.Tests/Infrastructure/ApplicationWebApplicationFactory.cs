using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectTemplate.Web.Tests.TestControllers;

namespace ProjectTemplate.Web.Tests.Infrastructure;

/// <summary>
/// Provides a custom WebApplicationFactory for integration testing, allowing configuration values to be injected and
/// the test environment to be set up with controllers from the specified assembly.
/// </summary>
/// <remarks>This factory sets the hosting environment to "Testing" and registers controllers from the assembly
/// containing RateLimitingTestController. Use this class to create test server instances with custom configuration for
/// integration tests.</remarks>
/// <param name="configurationValues">A read-only dictionary containing configuration key-value pairs to be applied to the test application's
/// configuration. Keys represent configuration paths; values may be null to clear a setting.</param>
internal sealed class ApplicationWebApplicationFactory(IReadOnlyDictionary<string, string?> configurationValues) : WebApplicationFactory<Program>
{
    private readonly IReadOnlyDictionary<string, string?> _configurationValues = configurationValues;

    private const string _requireAuthenticatedUserByDefaultKey =
        "ProjectTemplate:Authorization:RequireAuthenticatedUserByDefault";

    /// <summary>
    /// Creates a factory whose application allows anonymous requests, for tests that exercise middleware rather than
    /// authorization.
    /// </summary>
    /// <param name="configurationValues">Additional configuration values, applied after the anonymous-access setting.</param>
    /// <returns>A factory configured without the authorization fallback policy.</returns>
    /// <remarks>
    /// The factory otherwise matches the scaffold, which ships
    /// <c>RequireAuthenticatedUserByDefault</c> enabled, so a test that needs anonymous access says so here rather
    /// than inheriting it silently. Tests using this are asserting middleware behavior — security headers, rate
    /// limiting, error handling, API versioning — on endpoints a real application would expose anonymously.
    /// </remarks>
    internal static ApplicationWebApplicationFactory CreateAllowingAnonymousAccess(
        IReadOnlyDictionary<string, string?>? configurationValues = null)
    {
        Dictionary<string, string?> values = new()
        {
            [_requireAuthenticatedUserByDefaultKey] = "false"
        };

        foreach ((string key, string? value) in configurationValues ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        return new ApplicationWebApplicationFactory(values);
    }

    /// <summary>
    /// Configures the web host builder with test-specific settings, including environment, configuration, and services.
    /// </summary>
    /// <remarks>This method sets the environment to "Testing", adds in-memory configuration values, and
    /// registers controllers from the test assembly. Override this method to customize the web host for integration
    /// testing scenarios.</remarks>
    /// <param name="builder">The <see cref="IWebHostBuilder"/> to configure with the testing environment, in-memory configuration values, and
    /// required services.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // RequireAuthenticatedUserByDefault is deliberately not set here, so tests run under whatever the
        // application's own appsettings ships. That value is not fixed across scaffolds: generating with
        // --authProvider none produces both it and Authentication:Enabled as false, and setting it to true there
        // fails options validation at startup. Tests needing anonymous access opt out through
        // CreateAllowingAnonymousAccess, which sets false and is valid under either configuration.
        Dictionary<string, string?> testConfiguration = new()
        {
            ["ProjectTemplate:ForwardedHeaders:KnownProxies:0"] = "::1"
        };

        foreach ((string key, string? value) in _configurationValues)
        {
            testConfiguration[key] = value;
        }

        builder.ConfigureAppConfiguration((_, configurationBuilder) => configurationBuilder.AddInMemoryCollection(testConfiguration));

        builder.ConfigureServices(services => services
                .AddControllersWithViews()
                .AddApplicationPart(typeof(AdvertisedBehaviorTestController).Assembly));
    }
}
