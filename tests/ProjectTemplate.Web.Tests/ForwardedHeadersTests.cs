using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides tests for forwarded headers configuration and binding behavior.
/// </summary>
public sealed class ForwardedHeadersTests
{
    /// <summary>
    /// Verifies that X-Forwarded-For and X-Forwarded-Proto are enabled by default,
    /// while X-Forwarded-Host remains disabled by default.
    /// </summary>
    [Fact]
    public void ForwardedHeadersOptions_EnableExpectedHeadersByDefault()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>());

        ForwardedHeadersOptions options = GetForwardedHeadersOptions(factory);

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
    }

    /// <summary>
    /// Verifies that non-development deployments fail closed when forwarded client IP processing and rate limiting
    /// are enabled without an explicitly configured proxy trust boundary.
    /// </summary>
    [Fact]
    public void ApplicationForwardedHeadersOptions_RequireExplicitProxyTrustByDefault()
    {
        var options = new ApplicationForwardedHeadersOptions();

        Assert.True(options.RequireExplicitProxyTrust);
    }

    /// <summary>
    /// Verifies that forwarded headers processing is disabled when application forwarded headers are disabled.
    /// </summary>
    [Fact]
    public void ForwardedHeadersOptions_DisablesForwardedHeaders_WhenConfigurationIsDisabled()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:Enabled"] = "false"
        });

        ForwardedHeadersOptions options = GetForwardedHeadersOptions(factory);

        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    /// <summary>
    /// Verifies that configured known proxy IP addresses are bound into ASP.NET Core forwarded headers options.
    /// </summary>
    [Fact]
    public void ForwardedHeadersOptions_BindsKnownProxiesFromConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:ClearKnownNetworksAndProxies"] = "true",
            ["ProjectTemplate:ForwardedHeaders:KnownProxies:0"] = "203.0.113.10",
            ["ProjectTemplate:ForwardedHeaders:KnownProxies:1"] = "2001:db8::1"
        });

        ForwardedHeadersOptions options = GetForwardedHeadersOptions(factory);

        Assert.Equal(2, options.KnownProxies.Count);
        Assert.Contains(IPAddress.Parse("203.0.113.10"), options.KnownProxies);
        Assert.Contains(IPAddress.Parse("2001:db8::1"), options.KnownProxies);
    }

    /// <summary>
    /// Verifies that configured known proxy networks are bound into ASP.NET Core forwarded headers options.
    /// </summary>
    [Fact]
    public void ForwardedHeadersOptions_BindsKnownNetworksFromConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:ClearKnownNetworksAndProxies"] = "true",
            ["ProjectTemplate:ForwardedHeaders:KnownNetworks:0"] = "10.10.0.0/16",
            ["ProjectTemplate:ForwardedHeaders:KnownNetworks:1"] = "2001:db8::/64"
        });

        ForwardedHeadersOptions options = GetForwardedHeadersOptions(factory);

        Assert.Equal(2, options.KnownIPNetworks.Count);
        Assert.Contains(options.KnownIPNetworks, network => network.Contains(IPAddress.Parse("10.10.1.25")));
        Assert.Contains(options.KnownIPNetworks, network => network.Contains(IPAddress.Parse("2001:db8::1234")));
    }

    /// <summary>
    /// Verifies that forwarded host support and allowed hosts are explicitly configured together.
    /// </summary>
    [Fact]
    public void ForwardedHeadersOptions_BindsAllowedHosts_WhenForwardedHostIsEnabled()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:Headers:0"] = "XForwardedFor",
            ["ProjectTemplate:ForwardedHeaders:Headers:1"] = "XForwardedProto",
            ["ProjectTemplate:ForwardedHeaders:Headers:2"] = "XForwardedHost",
            ["ProjectTemplate:ForwardedHeaders:AllowedHosts:0"] = "app.example.com",
            ["ProjectTemplate:ForwardedHeaders:AllowedHosts:1"] = "www.example.com"
        });

        ForwardedHeadersOptions options = GetForwardedHeadersOptions(factory);

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
        Assert.Equal(["app.example.com", "www.example.com"], options.AllowedHosts);
    }

    /// <summary>
    /// Verifies that the application forwarded headers options model is bound from configuration.
    /// </summary>
    [Fact]
    public void ApplicationForwardedHeadersOptions_AreBoundFromConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:ForwardedHeaders:Enabled"] = "true",
            ["ProjectTemplate:ForwardedHeaders:Headers:0"] = "XForwardedFor",
            ["ProjectTemplate:ForwardedHeaders:Headers:1"] = "XForwardedProto",
            ["ProjectTemplate:ForwardedHeaders:ForwardLimit"] = "2",
            ["ProjectTemplate:ForwardedHeaders:RequireHeaderSymmetry"] = "true",
            ["ProjectTemplate:ForwardedHeaders:RequireExplicitProxyTrust"] = "false",
            ["ProjectTemplate:ForwardedHeaders:ClearKnownNetworksAndProxies"] = "true",
            ["ProjectTemplate:ForwardedHeaders:KnownProxies:0"] = "203.0.113.10",
            ["ProjectTemplate:ForwardedHeaders:KnownNetworks:0"] = "10.10.0.0/16"
        });

        ApplicationForwardedHeadersOptions options = factory.Services
            .GetRequiredService<IOptions<ApplicationForwardedHeadersOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Contains("XForwardedFor", options.Headers);
        Assert.Contains("XForwardedProto", options.Headers);
        Assert.Equal(2, options.ForwardLimit);
        Assert.True(options.RequireHeaderSymmetry);
        Assert.False(options.RequireExplicitProxyTrust);
        Assert.True(options.ClearKnownNetworksAndProxies);
        Assert.Equal(["203.0.113.10"], options.KnownProxies);
        Assert.Equal(["10.10.0.0/16"], options.KnownNetworks);
    }

    /// <summary>
    /// Verifies that startup validation fails when a configured known proxy is not a valid IP address.
    /// </summary>
    [Fact]
    public void ForwardedHeaders_InvalidProxyIp_FailsStartup()
    {
        OptionsValidationException exception =
            AssertOptionsValidationFails<ApplicationForwardedHeadersOptions>(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:ForwardedHeaders:KnownProxies:0"] = "not-an-ip-address"
                });

        Assert.Contains(
            "ProjectTemplate:ForwardedHeaders:KnownProxies must contain valid IP addresses",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when a configured known network is not a valid CIDR range.
    /// </summary>
    [Fact]
    public void ForwardedHeaders_InvalidKnownNetwork_FailsStartup()
    {
        OptionsValidationException exception =
            AssertOptionsValidationFails<ApplicationForwardedHeadersOptions>(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:ForwardedHeaders:KnownNetworks:0"] = "10.0.0.0"
                });

        Assert.Contains(
            "ProjectTemplate:ForwardedHeaders:KnownNetworks must contain valid CIDR ranges",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when X-Forwarded-Host is enabled without configured allowed hosts.
    /// </summary>
    [Fact]
    public void ForwardedHeaders_XForwardedHostWithoutAllowedHosts_FailsStartup()
    {
        OptionsValidationException exception =
            AssertOptionsValidationFails<ApplicationForwardedHeadersOptions>(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:ForwardedHeaders:Headers:0"] = "XForwardedFor",
                    ["ProjectTemplate:ForwardedHeaders:Headers:1"] = "XForwardedProto",
                    ["ProjectTemplate:ForwardedHeaders:Headers:2"] = "XForwardedHost"
                });

        Assert.Contains(
            "ProjectTemplate:ForwardedHeaders:AllowedHosts must contain at least one host when XForwardedHost is enabled",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a test application factory with the supplied in-memory configuration overrides.
    /// </summary>
    /// <param name="configurationValues">The configuration key/value pairs used to override application settings for a test.</param>
    /// <returns>A configured <see cref="ApplicationWebApplicationFactory"/> instance.</returns>
    private static ApplicationWebApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?> configurationValues)
    {
        return new ApplicationWebApplicationFactory(configurationValues);
    }

    /// <summary>
    /// Gets the configured ASP.NET Core forwarded headers options from the test application factory.
    /// </summary>
    /// <param name="factory">The test application factory.</param>
    /// <returns>The configured <see cref="ForwardedHeadersOptions"/> instance.</returns>
    private static ForwardedHeadersOptions GetForwardedHeadersOptions(ApplicationWebApplicationFactory factory)
    {
        return factory.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;
    }

    private static OptionsValidationException AssertOptionsValidationFails<TOptions>(
        IReadOnlyDictionary<string, string?> configurationValues)
        where TOptions : class
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(configurationValues);

        Exception? exception = Record.Exception(() =>
        {
            _ = factory.Services
                .GetRequiredService<IOptions<TOptions>>()
                .Value;
        });

        Assert.NotNull(exception);

        OptionsValidationException? optionsValidationException =
            FindOptionsValidationException(exception);

        Assert.NotNull(optionsValidationException);

        return optionsValidationException;
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
