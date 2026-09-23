using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides tests for replace semantics on configured path prefix lists.
/// </summary>
public sealed class ConfigurationListBindingTests
{
    private static readonly string[] _defaultSecurityHeaderExclusions = ["/health", "/metrics"];

    /// <summary>
    /// Verifies that configured entries replace the list contents.
    /// </summary>
    [Fact]
    public void ReplaceWithConfiguredValues_ConfiguredEntries_ReplaceDefaults()
    {
        List<string> target = ["/health", "/metrics"];
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["List:0"] = "/status"
        });

        ConfigurationListBinding.ReplaceWithConfiguredValues(target, configuration.GetSection("List"));

        Assert.Equal("/status", Assert.Single(target));
    }

    /// <summary>
    /// Verifies that the defaults are kept when the section has no entries.
    /// </summary>
    [Fact]
    public void ReplaceWithConfiguredValues_NoEntries_KeepsDefaults()
    {
        List<string> target = ["/health", "/metrics"];
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>());

        ConfigurationListBinding.ReplaceWithConfiguredValues(target, configuration.GetSection("List"));

        Assert.Equal(_defaultSecurityHeaderExclusions, target);
    }

    /// <summary>
    /// Verifies that a later configuration source can remove an inherited entry by overriding it with a blank value.
    /// </summary>
    [Fact]
    public void ReplaceWithConfiguredValues_BlankOverride_RemovesInheritedEntry()
    {
        List<string> target = [];
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["List:0"] = "/health",
                ["List:1"] = "/metrics"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["List:1"] = string.Empty
            })
            .Build();

        ConfigurationListBinding.ReplaceWithConfiguredValues(target, configuration.GetSection("List"));

        Assert.Equal("/health", Assert.Single(target));
    }

    /// <summary>
    /// Verifies that configured security header exclusions replace the code defaults instead of being appended.
    /// </summary>
    [Fact]
    public void AddApplicationSecurityHeaders_ConfiguredExclusions_ReplaceDefaults()
    {
        ApplicationSecurityHeadersOptions options = GetSecurityHeadersOptions(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:0"] = "/status"
        });

        Assert.Equal("/status", Assert.Single(options.ExcludedPathPrefixes));
    }

    /// <summary>
    /// Verifies that configuring the same entries as the defaults does not duplicate them.
    /// </summary>
    [Fact]
    public void AddApplicationSecurityHeaders_ConfiguredDefaults_AreNotDuplicated()
    {
        ApplicationSecurityHeadersOptions options = GetSecurityHeadersOptions(new Dictionary<string, string?>
        {
            ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:0"] = "/health",
            ["ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes:1"] = "/metrics"
        });

        Assert.Equal(_defaultSecurityHeaderExclusions, options.ExcludedPathPrefixes);
    }

    /// <summary>
    /// Verifies that security header exclusions keep the code defaults when nothing is configured.
    /// </summary>
    [Fact]
    public void AddApplicationSecurityHeaders_NoConfiguredExclusions_KeepsDefaults()
    {
        ApplicationSecurityHeadersOptions options = GetSecurityHeadersOptions(new Dictionary<string, string?>());

        Assert.Equal(_defaultSecurityHeaderExclusions, options.ExcludedPathPrefixes);
    }

    /// <summary>
    /// Verifies that configured request logging exclusions replace the code defaults instead of being appended.
    /// </summary>
    [Fact]
    public void AddApplicationRequestLogging_ConfiguredExclusions_ReplaceDefaults()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:0"] = "/healthz"
        });
        ServiceCollection services = new();
        _ = services.AddApplicationRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        ApplicationRequestLoggingOptions options = provider
            .GetRequiredService<IOptions<ApplicationRequestLoggingOptions>>()
            .Value;

        Assert.Equal("/healthz", Assert.Single(options.ExcludedPathPrefixes));
    }

    private static ApplicationSecurityHeadersOptions GetSecurityHeadersOptions(
        IReadOnlyDictionary<string, string?> values)
    {
        ServiceCollection services = new();
        _ = services.AddApplicationSecurityHeaders(CreateConfiguration(values));

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider
            .GetRequiredService<IOptions<ApplicationSecurityHeadersOptions>>()
            .Value;
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
