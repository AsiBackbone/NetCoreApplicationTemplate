using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class OpenTelemetryServiceExtensionsCoverageTests
{
    /// <summary>
    /// Verifies that when OpenTelemetry is disabled, the service collection is returned without modification and no exceptions are thrown.
    /// </summary>
    [Fact]
    public void AddApplicationOpenTelemetry_Disabled_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Enabled"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceName"] = "ProjectTemplate.Web.Tests"
        });

        IServiceCollection result = services.AddApplicationOpenTelemetry(
            configuration,
            new TestHostEnvironment());

        Assert.Same(services, result);
        Assert.NotEmpty(services);
    }

    /// <summary>
    /// Verifies that when OpenTelemetry is enabled but both tracing and metrics are disabled, the service collection is returned without modification and no exceptions are thrown.
    /// </summary>
    [Fact]
    public void AddApplicationOpenTelemetry_EnabledWithoutTracingOrMetrics_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();

        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Enabled"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceName"] = "ProjectTemplate.Web.Tests",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceVersion"] = " 2.1.0 ",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableTracing"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableMetrics"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableAspNetCoreInstrumentation"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableHttpClientInstrumentation"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Enabled"] = "false"
        });

        IServiceCollection result = services.AddApplicationOpenTelemetry(
            configuration,
            new TestHostEnvironment { EnvironmentName = Environments.Production });

        Assert.Same(services, result);
        Assert.NotEmpty(services);
    }

    /// <summary>
    /// Verifies that when OpenTelemetry is enabled with both tracing and metrics enabled, and OTLP export is configured, the service collection is returned without modification and no exceptions are thrown.
    /// </summary>
    [Fact]
    public void AddApplicationOpenTelemetry_TracingMetricsAndOtlpEnabled_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();

        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Enabled"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceName"] = "ProjectTemplate.Web.Tests",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceVersion"] = "2.1.0",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableTracing"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableMetrics"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableAspNetCoreInstrumentation"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:EnableHttpClientInstrumentation"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Enabled"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Endpoint"] = "http://localhost:4317",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Protocol"] = "HttpProtobuf"
        });

        IServiceCollection result = services.AddApplicationOpenTelemetry(
            configuration,
            new TestHostEnvironment { EnvironmentName = "Testing" });

        Assert.Same(services, result);
        Assert.NotEmpty(services);
    }

    /// <summary>
    /// Verifies that when OpenTelemetry is enabled but the service name is blank, an OptionsValidationException is thrown indicating that the service name must not be empty.
    /// </summary>
    [Fact]
    public void AddApplicationOpenTelemetry_BlankServiceName_FailsOptionsValidation()
    {
        ServiceCollection services = new();

        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Enabled"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceName"] = " "
        });

        _ = services.AddApplicationOpenTelemetry(configuration, new TestHostEnvironment());

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ApplicationOpenTelemetryOptions>>().Value);

        Assert.Contains(
            "ProjectTemplate:OpenTelemetry:ServiceName must not be empty.",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that when OpenTelemetry is enabled and OTLP export is enabled but the endpoint is not a valid absolute URI, an OptionsValidationException is thrown indicating that the endpoint must be a valid absolute URI when OTLP export is enabled.
    /// </summary>
    [Fact]
    public void AddApplicationOpenTelemetry_InvalidOtlpEndpoint_FailsOptionsValidation()
    {
        ServiceCollection services = new();

        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Enabled"] = "false",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:ServiceName"] = "ProjectTemplate.Web.Tests",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Enabled"] = "true",
            [$"{ApplicationOpenTelemetryOptions.SectionName}:Otlp:Endpoint"] = "not-a-valid-uri"
        });

        _ = services.AddApplicationOpenTelemetry(configuration, new TestHostEnvironment());

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ApplicationOpenTelemetryOptions>>().Value);

        Assert.Contains(
            "ProjectTemplate:OpenTelemetry:Otlp:Endpoint must be a valid absolute URI when OTLP export is enabled.",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that when an explicit service version is provided with leading and trailing whitespace, the resolved service version is trimmed of whitespace.
    /// </summary>
    [Fact]
    public void ResolveServiceVersion_ExplicitVersion_ReturnsTrimmedVersion()
    {
        ApplicationOpenTelemetryOptions options = new()
        {
            ServiceVersion = " 2.6.0 "
        };

        string result = InvokeResolveServiceVersion(options);

        Assert.Equal("2.6.0", result);
    }

    /// <summary>
    /// Verifies that when the service version is missing (null, empty, or whitespace), the resolved service version falls back to the assembly version of the OpenTelemetryServiceExtensions class.
    /// </summary>
    /// <param name="serviceVersion">
    /// Null, empty, or whitespace string representing the service version to test. The test will be run for each of these cases to ensure that the fallback logic is correctly applied when the service version is not provided.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ResolveServiceVersion_MissingVersion_ReturnsAssemblyFallback(string? serviceVersion)
    {
        ApplicationOpenTelemetryOptions options = new()
        {
            ServiceVersion = serviceVersion
        };

        string result = InvokeResolveServiceVersion(options);

        Assert.Equal(GetExpectedAssemblyVersion(), result);
    }

    /// <summary>
    /// Verifies that the ConfigureOtlpExporter method correctly configures the OtlpExporterOptions based on the provided ApplicationOtlpExporterOptions, including parsing the protocol string and setting the endpoint URI. The test covers various protocol string cases (e.g., "HttpProtobuf", "httpprotobuf", "Grpc", and an unknown value) to ensure that the correct OtlpExportProtocol enum value is set in the exporter options. Additionally, it verifies that the endpoint is correctly parsed into a Uri object.
    /// </summary>
    /// <param name="protocol">
    /// The protocol string to test, which will be parsed by the ConfigureOtlpExporter method to determine the OtlpExportProtocol to use. The test includes valid protocol strings in different cases (e.g., "HttpProtobuf" and "httpprotobuf") as well as an unknown value to verify that the default protocol is used when an unrecognized string is provided.
    /// </param>
    /// <param name="expectedProtocol">
    /// The expected OtlpExportProtocol enum value that should be set in the OtlpExporterOptions based on the provided protocol string. This parameter allows the test to verify that the ConfigureOtlpExporter method correctly maps the input protocol string to the corresponding OtlpExportProtocol value, ensuring that the exporter is configured with the intended protocol for OTLP export.
    /// </param>
    [Theory]
    [InlineData("HttpProtobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("httpprotobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("Grpc", OtlpExportProtocol.Grpc)]
    [InlineData("unknown", OtlpExportProtocol.Grpc)]
    public void ConfigureOtlpExporter_ConfiguresEndpointAndProtocol(
        string protocol,
        OtlpExportProtocol expectedProtocol)
    {
        ApplicationOtlpExporterOptions options = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:4317",
            Protocol = protocol
        };

        OtlpExporterOptions exporterOptions = InvokeConfigureOtlpExporter(options);

        Assert.Equal(new Uri("http://localhost:4317"), exporterOptions.Endpoint);
        Assert.Equal(expectedProtocol, exporterOptions.Protocol);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string InvokeResolveServiceVersion(ApplicationOpenTelemetryOptions options)
    {
        MethodInfo? method = typeof(OpenTelemetryServiceExtensions).GetMethod(
            "ResolveServiceVersion",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null);

        object? result = method.Invoke(null, [options]);

        return Assert.IsType<string>(result);
    }

    private static OtlpExporterOptions InvokeConfigureOtlpExporter(
        ApplicationOtlpExporterOptions options)
    {
        OtlpExporterOptions exporterOptions = new();

        MethodInfo? method = typeof(OpenTelemetryServiceExtensions).GetMethod(
            "ConfigureOtlpExporter",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null);

        _ = method.Invoke(null, [exporterOptions, options]);

        return exporterOptions;
    }

    private static string GetExpectedAssemblyVersion()
    {
        Assembly assembly = typeof(OpenTelemetryServiceExtensions).Assembly;

        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion
            : assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
