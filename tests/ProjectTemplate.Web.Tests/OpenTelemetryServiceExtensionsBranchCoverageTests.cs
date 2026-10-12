using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the missing-configuration-section branch of <c>OpenTelemetryServiceExtensions.AddApplicationOpenTelemetry</c>.
/// </summary>
/// <remarks>
/// The informational-version and assembly-version fallbacks in <c>ResolveServiceVersion</c> read attributes of the
/// compiled <c>ProjectTemplate.Web</c> assembly, which always carries an informational version, so those branches
/// are not reachable from tests.
/// </remarks>
public sealed class OpenTelemetryServiceExtensionsBranchCoverageTests
{
    [Fact]
    public void AddApplicationOpenTelemetry_MissingConfigurationSection_UsesDefaultOptions()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        IServiceCollection result = services.AddApplicationOpenTelemetry(configuration, new TestHostEnvironment());

        using ServiceProvider provider = services.BuildServiceProvider();
        ApplicationOpenTelemetryOptions options = provider
            .GetRequiredService<IOptions<ApplicationOpenTelemetryOptions>>()
            .Value;
        Assert.Same(services, result);
        Assert.True(options.Enabled);
        Assert.Equal("ProjectTemplate.Web", options.ServiceName);
        Assert.False(options.Otlp.Enabled);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
