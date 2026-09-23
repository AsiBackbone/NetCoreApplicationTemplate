using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class RequestLoggingExtensionsBranchGapTests
{
    [Fact]
    public void AddApplicationRequestLogging_BlankCorrelationHeaderName_FailsOptionsValidation()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationRequestLoggingOptions.SectionName}:CorrelationHeaderName"] = " "
        });

        _ = services.AddApplicationRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ApplicationRequestLoggingOptions>>().Value);

        Assert.Contains(
            "ProjectTemplate:RequestLogging:CorrelationHeaderName is required.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("health")]
    [InlineData("metrics/")]
    public void AddApplicationRequestLogging_InvalidExcludedPathPrefix_FailsOptionsValidation(string excludedPrefix)
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationRequestLoggingOptions.SectionName}:CorrelationHeaderName"] = "X-Correlation-ID",
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:0"] = excludedPrefix
        });

        _ = services.AddApplicationRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ApplicationRequestLoggingOptions>>().Value);

        Assert.Contains(
            "ProjectTemplate:RequestLogging:ExcludedPathPrefixes values must start with '/'.",
            exception.Message,
            StringComparison.Ordinal);
    }

    // Blank entries are ignored rather than rejected, so a later configuration source can remove an inherited
    // prefix by overriding its index with an empty value. See ConfigurationListBinding.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddApplicationRequestLogging_BlankExcludedPathPrefix_IsIgnored(string excludedPrefix)
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationRequestLoggingOptions.SectionName}:CorrelationHeaderName"] = "X-Correlation-ID",
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:0"] = "/health",
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:1"] = excludedPrefix
        });

        _ = services.AddApplicationRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        ApplicationRequestLoggingOptions options = provider
            .GetRequiredService<IOptions<ApplicationRequestLoggingOptions>>()
            .Value;

        Assert.Equal("/health", Assert.Single(options.ExcludedPathPrefixes));
    }

    [Fact]
    public void AddApplicationRequestLogging_ValidExcludedPathPrefixes_BindsOptions()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{ApplicationRequestLoggingOptions.SectionName}:Enabled"] = "true",
            [$"{ApplicationRequestLoggingOptions.SectionName}:CorrelationHeaderName"] = "X-Test-Correlation-ID",
            [$"{ApplicationRequestLoggingOptions.SectionName}:IncludeQueryString"] = "true",
            [$"{ApplicationRequestLoggingOptions.SectionName}:IncludeRemoteIpAddress"] = "false",
            [$"{ApplicationRequestLoggingOptions.SectionName}:IncludeUserName"] = "false",
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:0"] = "/healthz",
            [$"{ApplicationRequestLoggingOptions.SectionName}:ExcludedPathPrefixes:1"] = "/metrics"
        });

        _ = services.AddApplicationRequestLogging(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        ApplicationRequestLoggingOptions options = provider
            .GetRequiredService<IOptions<ApplicationRequestLoggingOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.Equal("X-Test-Correlation-ID", options.CorrelationHeaderName);
        Assert.True(options.IncludeQueryString);
        Assert.False(options.IncludeRemoteIpAddress);
        Assert.False(options.IncludeUserName);
        Assert.Contains("/healthz", options.ExcludedPathPrefixes);
        Assert.Contains("/metrics", options.ExcludedPathPrefixes);
    }

    [Fact]
    public void GetCorrelationId_HeaderWithWhitespace_ReturnsTrimmedHeaderValue()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-fallback"
        };
        httpContext.Request.Headers["X-Correlation-ID"] = "  request-correlation-id  ";

        string result = InvokeGetCorrelationId(httpContext, new ApplicationRequestLoggingOptions());

        Assert.Equal("request-correlation-id", result);
    }

    [Fact]
    public void GetCorrelationId_HeaderLongerThanMaximum_TruncatesToMaximumLength()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-fallback"
        };
        httpContext.Request.Headers["X-Correlation-ID"] = new string('a', 140);

        string result = InvokeGetCorrelationId(httpContext, new ApplicationRequestLoggingOptions());

        Assert.Equal(128, result.Length);
        Assert.Equal(new string('a', 128), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCorrelationId_MissingOrBlankHeader_ReturnsTraceIdentifier(string? headerValue)
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-fallback"
        };

        if (headerValue is not null)
        {
            httpContext.Request.Headers["X-Correlation-ID"] = headerValue;
        }

        string result = InvokeGetCorrelationId(httpContext, new ApplicationRequestLoggingOptions());

        Assert.Equal("trace-fallback", result);
    }

    [Theory]
    [InlineData("/HEALTH/ready", true)]
    [InlineData("/metrics/runtime", true)]
    [InlineData("/api/status", false)]
    [InlineData("", false)]
    public void IsExcludedPath_ReturnsExpectedMatchForConfiguredPrefixes(
        string requestPath,
        bool expectedResult)
    {
        ApplicationRequestLoggingOptions options = new()
        {
            ExcludedPathPrefixes =
            [
                "/health",
                "/metrics"
            ]
        };

        bool result = InvokeIsExcludedPath(new PathString(requestPath), options);

        Assert.Equal(expectedResult, result);
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static string InvokeGetCorrelationId(
        HttpContext httpContext,
        ApplicationRequestLoggingOptions options)
    {
        MethodInfo? method = typeof(RequestLoggingExtensions).GetMethod(
            "GetCorrelationId",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null);

        object? result = method.Invoke(null, [httpContext, options]);

        return Assert.IsType<string>(result);
    }

    private static bool InvokeIsExcludedPath(
        PathString requestPath,
        ApplicationRequestLoggingOptions options)
    {
        MethodInfo? method = typeof(RequestLoggingExtensions).GetMethod(
            "IsExcludedPath",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(method is not null);

        object? result = method.Invoke(null, [requestPath, options]);

        return Assert.IsType<bool>(result);
    }
}
