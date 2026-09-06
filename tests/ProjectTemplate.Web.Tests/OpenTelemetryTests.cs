using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration and configuration tests for the application OpenTelemetry foundation.
/// </summary>
public sealed class OpenTelemetryTests
{
    private const string _releaseVersion = "0.4.0";

    /// <summary>
    /// Verifies that application OpenTelemetry options are bound from configuration.
    /// </summary>
    [Fact]
    public void OpenTelemetryOptions_Bind_DefaultConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:OpenTelemetry:Enabled"] = "true",
            ["ProjectTemplate:OpenTelemetry:ServiceName"] = "ProjectTemplate.Web",
            ["ProjectTemplate:OpenTelemetry:ServiceVersion"] = _releaseVersion,
            ["ProjectTemplate:OpenTelemetry:EnableTracing"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableMetrics"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableAspNetCoreInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableHttpClientInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:Otlp:Enabled"] = "false",
            ["ProjectTemplate:OpenTelemetry:Otlp:Endpoint"] = "",
            ["ProjectTemplate:OpenTelemetry:Otlp:Protocol"] = "Grpc"
        });

        ApplicationOpenTelemetryOptions options = factory.Services
            .GetRequiredService<IOptions<ApplicationOpenTelemetryOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal("ProjectTemplate.Web", options.ServiceName);
        Assert.Equal(_releaseVersion, options.ServiceVersion);
        Assert.True(options.EnableTracing);
        Assert.True(options.EnableMetrics);
        Assert.True(options.EnableAspNetCoreInstrumentation);
        Assert.True(options.EnableHttpClientInstrumentation);

        Assert.False(options.Otlp.Enabled);
        Assert.Equal(string.Empty, options.Otlp.Endpoint);
        Assert.Equal("Grpc", options.Otlp.Protocol);
    }

    /// <summary>
    /// Verifies that OpenTelemetry options validation fails when OTLP export is enabled with an invalid endpoint.
    /// </summary>
    [Fact]
    public void OpenTelemetryOptions_InvalidOtlpEndpoint_FailsValidationWhenExporterEnabled()
    {
        OptionsValidationException exception =
            AssertOpenTelemetryOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:OpenTelemetry:Enabled"] = "true",
                    ["ProjectTemplate:OpenTelemetry:ServiceName"] = "ProjectTemplate.Web",
                    ["ProjectTemplate:OpenTelemetry:ServiceVersion"] = _releaseVersion,
                    ["ProjectTemplate:OpenTelemetry:EnableTracing"] = "true",
                    ["ProjectTemplate:OpenTelemetry:EnableMetrics"] = "true",
                    ["ProjectTemplate:OpenTelemetry:EnableAspNetCoreInstrumentation"] = "true",
                    ["ProjectTemplate:OpenTelemetry:EnableHttpClientInstrumentation"] = "true",
                    ["ProjectTemplate:OpenTelemetry:Otlp:Enabled"] = "true",
                    ["ProjectTemplate:OpenTelemetry:Otlp:Endpoint"] = "not-a-valid-uri",
                    ["ProjectTemplate:OpenTelemetry:Otlp:Protocol"] = "Grpc"
                });

        Assert.Contains(
            "ProjectTemplate:OpenTelemetry:Otlp:Endpoint must be a valid absolute URI when OTLP export is enabled",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the application starts when OpenTelemetry is enabled and OTLP export is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Application_Starts_WhenOpenTelemetryEnabledWithoutOtlpExporter()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:OpenTelemetry:Enabled"] = "true",
            ["ProjectTemplate:OpenTelemetry:ServiceName"] = "ProjectTemplate.Web",
            ["ProjectTemplate:OpenTelemetry:ServiceVersion"] = _releaseVersion,
            ["ProjectTemplate:OpenTelemetry:EnableTracing"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableMetrics"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableAspNetCoreInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableHttpClientInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:Otlp:Enabled"] = "false",
            ["ProjectTemplate:OpenTelemetry:Otlp:Endpoint"] = "",
            ["ProjectTemplate:OpenTelemetry:Otlp:Protocol"] = "Grpc"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the application starts when OpenTelemetry is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task Application_Starts_WhenOpenTelemetryDisabled()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:OpenTelemetry:Enabled"] = "false",
            ["ProjectTemplate:OpenTelemetry:ServiceName"] = "ProjectTemplate.Web",
            ["ProjectTemplate:OpenTelemetry:ServiceVersion"] = _releaseVersion,
            ["ProjectTemplate:OpenTelemetry:EnableTracing"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableMetrics"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableAspNetCoreInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:EnableHttpClientInstrumentation"] = "true",
            ["ProjectTemplate:OpenTelemetry:Otlp:Enabled"] = "false",
            ["ProjectTemplate:OpenTelemetry:Otlp:Endpoint"] = "",
            ["ProjectTemplate:OpenTelemetry:Otlp:Protocol"] = "Grpc"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

    private static OptionsValidationException AssertOpenTelemetryOptionsValidationFails(
        IReadOnlyDictionary<string, string?> configurationValues)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        IServiceCollection services = new ServiceCollection();

        _ = services.AddApplicationOpenTelemetry(
            configuration,
            new TestHostEnvironment());

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        return Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<ApplicationOpenTelemetryOptions>>()
                .Value);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
