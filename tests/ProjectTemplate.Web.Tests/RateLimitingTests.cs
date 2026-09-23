using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Diagnostics;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.Options;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;
using ProjectTemplate.Web.Tests.TestControllers;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for the application rate limiting configuration and policies.
/// </summary>
public sealed class RateLimitingTests
{
    /// <summary>
    /// Verifies that the global fixed-window limiter rejects requests after the configured permit limit is exceeded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task GlobalFixedWindowLimiter_ReturnsTooManyRequests_WhenPermitLimitIsExceeded()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "true",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "true",
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "true",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit"] = "1",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds"] = "60",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit"] = "0"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage firstResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that the named fixed-window policy rejects endpoint requests after the configured permit limit is exceeded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task NamedFixedWindowPolicy_ReturnsTooManyRequests_WhenPermitLimitIsExceeded()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "true",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "false",
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "true",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:PermitLimit"] = "1",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:WindowSeconds"] = "60",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:QueueLimit"] = "0"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage firstResponse = await client.GetAsync("/test/rate-limiting/fixed", TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await client.GetAsync("/test/rate-limiting/fixed", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that the named concurrency policy rejects a second request while the configured permit is already in use.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task NamedConcurrencyPolicy_ReturnsTooManyRequests_WhenConcurrentLimitIsExceeded()
    {
        RateLimitingTestController.ResetConcurrencySignal();

        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "true",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "false",
            // The test server supplies no client address. Both requests must share one fallback partition, because
            // the concurrency policy now partitions by client as well as by endpoint.
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "true",
            ["ProjectTemplate:RateLimiting:ConcurrencyPolicy:PermitLimit"] = "1",
            ["ProjectTemplate:RateLimiting:ConcurrencyPolicy:QueueLimit"] = "0"
        });

        using HttpClient client = factory.CreateHttpsClient();

        Task<HttpResponseMessage> firstRequest = client.GetAsync("/test/rate-limiting/concurrency", TestContext.Current.CancellationToken);

        await RateLimitingTestController.WaitForConcurrencyRequestStartedAsync();

        using HttpResponseMessage secondResponse = await client.GetAsync("/test/rate-limiting/concurrency", TestContext.Current.CancellationToken);
        using HttpResponseMessage firstResponse = await firstRequest;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that API-shaped rejected requests return a Problem Details 429 response with the shared identifiers.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RejectedRequest_ApiShaped_ReturnsProblemDetails()
    {
        using ApplicationWebApplicationFactory factory = CreateSinglePermitGlobalLimiterFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage firstResponse = await SendWithAcceptAsync(client, "application/json");
        using HttpResponseMessage rejectedResponse = await SendWithAcceptAsync(client, "application/json");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.Equal("application/problem+json", rejectedResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter);

        using var document = JsonDocument.Parse(await rejectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;

        Assert.Equal(429, root.GetProperty("status").GetInt32());
        Assert.Equal(RateLimitingServiceExtensions.RejectionTitle, root.GetProperty("title").GetString());
        Assert.Equal(RateLimitingServiceExtensions.RejectionProblemType, root.GetProperty("type").GetString());
        Assert.Equal(RateLimitingServiceExtensions.RejectionDetail, root.GetProperty("detail").GetString());
        Assert.Equal("/", root.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("requestId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
        Assert.False(root.TryGetProperty("error", out _));
    }

    /// <summary>
    /// Verifies that browser-shaped rejected requests receive a short plain-text 429 response rather than a rendered error page.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RejectedRequest_BrowserShaped_ReturnsPlainTextWithoutRenderingErrorPage()
    {
        using ApplicationWebApplicationFactory factory = CreateSinglePermitGlobalLimiterFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage firstResponse = await SendWithAcceptAsync(client, "text/html");
        using HttpResponseMessage rejectedResponse = await SendWithAcceptAsync(client, "text/html");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.Equal("text/plain", rejectedResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejectedResponse.Headers.RetryAfter);
        Assert.Equal(
            RateLimitingServiceExtensions.RejectionDetail,
            await rejectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that the rejection log entry omits the remote IP address by default.
    /// </summary>
    [Fact]
    public void LogRejectedRequest_OmitsRemoteIpAddress_ByDefault()
    {
        using ServiceProvider services = CreateRequestLoggingServices(new ApplicationRequestLoggingOptions());
        DefaultHttpContext httpContext = CreateRejectedHttpContext(services);
        TestLogger logger = new();

        RateLimitingServiceExtensions.LogRejectedRequest(httpContext, logger, TimeSpan.FromSeconds(30));

        LogEntry entry = Assert.Single(logger.Entries);

        Assert.Equal(6100, entry.EventId.Id);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("203.0.113.10", entry.Message, StringComparison.Ordinal);
        Assert.Contains("TraceIdentifier: trace-rejected", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the rejection log entry includes the remote IP address only when request logging opts in.
    /// </summary>
    [Fact]
    public void LogRejectedRequest_IncludesRemoteIpAddress_WhenRequestLoggingOptsIn()
    {
        using ServiceProvider services = CreateRequestLoggingServices(
            new ApplicationRequestLoggingOptions { IncludeRemoteIpAddress = true });
        DefaultHttpContext httpContext = CreateRejectedHttpContext(services);
        TestLogger logger = new();

        RateLimitingServiceExtensions.LogRejectedRequest(httpContext, logger, TimeSpan.FromSeconds(30));

        LogEntry entry = Assert.Single(logger.Entries);

        Assert.Contains("RemoteIpAddress: 203.0.113.10", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that repeated requests are allowed when application rate limiting is disabled.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DisabledRateLimiting_DoesNotRejectRepeatedRequests()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "false",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "true",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit"] = "1",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds"] = "60",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit"] = "0"
        });

        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage firstResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    /// <summary>
    /// Verifies that application rate limiting options are bound from configuration into the options model.
    /// </summary>
    [Fact]
    public void RateLimitingOptions_AreBoundFromConfiguration()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "true",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "true",
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "false",
            ["ProjectTemplate:RateLimiting:UnknownClientPartitionKey"] = "configured-unknown-client",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit"] = "7",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds"] = "30",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit"] = "2",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:PermitLimit"] = "5",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:WindowSeconds"] = "20",
            ["ProjectTemplate:RateLimiting:FixedWindowPolicy:QueueLimit"] = "1",
            ["ProjectTemplate:RateLimiting:ConcurrencyPolicy:PermitLimit"] = "3",
            ["ProjectTemplate:RateLimiting:ConcurrencyPolicy:QueueLimit"] = "1"
        });

        ApplicationRateLimitingOptions options = factory.Services
            .GetRequiredService<IOptions<ApplicationRateLimitingOptions>>()
            .Value;

        Assert.True(options.Enabled);
        Assert.True(options.UseGlobalLimiter);
        Assert.False(options.UseSharedUnknownClientPartition);
        Assert.Equal("configured-unknown-client", options.UnknownClientPartitionKey);

        Assert.Equal(7, options.GlobalFixedWindow.PermitLimit);
        Assert.Equal(30, options.GlobalFixedWindow.WindowSeconds);
        Assert.Equal(2, options.GlobalFixedWindow.QueueLimit);

        Assert.Equal(5, options.FixedWindowPolicy.PermitLimit);
        Assert.Equal(20, options.FixedWindowPolicy.WindowSeconds);
        Assert.Equal(1, options.FixedWindowPolicy.QueueLimit);

        Assert.Equal(3, options.ConcurrencyPolicy.PermitLimit);
        Assert.Equal(1, options.ConcurrencyPolicy.QueueLimit);
    }

    /// <summary>
    /// Verifies that client rate limiting uses the resolved remote IP address when available.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_ReturnsRemoteIpAddress_WhenAvailable()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        TestLogger logger = new();

        string partitionKey = RateLimitingServiceExtensions.GetClientPartitionKey(
            httpContext,
            new ApplicationRateLimitingOptions(),
            logger);

        Assert.Equal("203.0.113.10", partitionKey);
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Verifies that the rate limiting options default to a shared fallback partition for unresolved clients.
    /// </summary>
    [Fact]
    public void RateLimitingOptions_CodeDefault_UsesSharedUnknownClientPartition()
    {
        ApplicationRateLimitingOptions options = new();

        Assert.True(options.UseSharedUnknownClientPartition);
        Assert.Equal("unknown-client", options.UnknownClientPartitionKey);
    }

    /// <summary>
    /// Verifies that unresolved client IP addresses share one fallback partition by default so they remain rate limited.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_UsesSharedFallback_WhenRemoteIpAddressIsUnavailableByDefault()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-331"
        };
        TestLogger logger = new();

        string partitionKey = RateLimitingServiceExtensions.GetClientPartitionKey(
            httpContext,
            new ApplicationRateLimitingOptions(),
            logger);

        LogEntry entry = Assert.Single(logger.Entries);

        Assert.Equal("unknown-client", partitionKey);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(6002, entry.EventId.Id);
        Assert.Contains("RemoteIpAddress was unavailable", entry.Message, StringComparison.Ordinal);
        Assert.Contains("FallbackMode: Shared", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that unresolved client IP addresses share the configured fallback partition key.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_UsesConfiguredSharedFallbackKey()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-331-shared"
        };
        TestLogger logger = new();
        ApplicationRateLimitingOptions options = new()
        {
            UnknownClientPartitionKey = "configured-unknown-client"
        };

        string partitionKey = RateLimitingServiceExtensions.GetClientPartitionKey(
            httpContext,
            options,
            logger);

        LogEntry entry = Assert.Single(logger.Entries);

        Assert.Equal("configured-unknown-client", partitionKey);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(6002, entry.EventId.Id);
        Assert.Contains("FallbackMode: Shared", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that unresolved client IP addresses only receive per-request fallback partitions when explicitly configured.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_UsesPerRequestFallback_WhenExplicitlyConfigured()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-331-per-request"
        };
        TestLogger logger = new();
        ApplicationRateLimitingOptions options = new()
        {
            UseSharedUnknownClientPartition = false
        };

        string partitionKey = RateLimitingServiceExtensions.GetClientPartitionKey(
            httpContext,
            options,
            logger);

        LogEntry entry = Assert.Single(logger.Entries);

        Assert.Equal("unknown-client:trace-331-per-request", partitionKey);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(6002, entry.EventId.Id);
        Assert.Contains("FallbackMode: PerRequest", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the fallback warning is written at most once per throttle interval and reports suppressed occurrences.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_ThrottlesFallbackWarning_AndReportsSuppressedCount()
    {
        ManualTimestampProvider timeProvider = new();
        RateLimitingFallbackWarningThrottle throttle = new(timeProvider, TimeSpan.FromMinutes(1));
        ApplicationRateLimitingOptions options = new();
        TestLogger logger = new();

        for (int requestNumber = 0; requestNumber < 3; requestNumber++)
        {
            _ = RateLimitingServiceExtensions.GetClientPartitionKey(
                new DefaultHttpContext { TraceIdentifier = $"trace-throttle-{requestNumber}" },
                options,
                logger,
                throttle);
        }

        LogEntry firstEntry = Assert.Single(logger.Entries);
        Assert.Contains("SuppressedWarningCount: 0", firstEntry.Message, StringComparison.Ordinal);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        _ = RateLimitingServiceExtensions.GetClientPartitionKey(
            new DefaultHttpContext { TraceIdentifier = "trace-throttle-after-interval" },
            options,
            logger,
            throttle);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains("SuppressedWarningCount: 2", logger.Entries[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the fallback warning throttle rejects a non-positive interval.
    /// </summary>
    [Fact]
    public void RateLimitingFallbackWarningThrottle_NonPositiveInterval_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RateLimitingFallbackWarningThrottle(TimeProvider.System, TimeSpan.Zero));
    }

    /// <summary>
    /// Verifies that startup validation fails when the unknown client fallback partition key is empty.
    /// </summary>
    [Fact]
    public void RateLimiting_EmptyUnknownClientPartitionKey_FailsStartup()
    {
        OptionsValidationException exception =
            AssertRateLimitingOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:RateLimiting:Enabled"] = "true",
                    ["ProjectTemplate:RateLimiting:UnknownClientPartitionKey"] = " "
                });

        Assert.Contains(
            "ProjectTemplate:RateLimiting:UnknownClientPartitionKey must not be empty",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when a fixed-window permit limit is zero.
    /// </summary>
    [Fact]
    public void RateLimiting_ZeroPermitLimit_FailsStartup()
    {
        OptionsValidationException exception =
            AssertRateLimitingOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:RateLimiting:Enabled"] = "true",
                    ["ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit"] = "0"
                });

        Assert.Contains(
            "ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit must be greater than zero",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when a rate-limiting queue limit is negative.
    /// </summary>
    [Fact]
    public void RateLimiting_NegativeQueueLimit_FailsStartup()
    {
        OptionsValidationException exception =
            AssertRateLimitingOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:RateLimiting:Enabled"] = "true",
                    ["ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit"] = "-1"
                });

        Assert.Contains(
            "ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit must be zero or greater",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that startup validation fails when a fixed-window duration is zero seconds.
    /// </summary>
    [Fact]
    public void RateLimiting_ZeroWindowSeconds_FailsStartup()
    {
        OptionsValidationException exception =
            AssertRateLimitingOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:RateLimiting:Enabled"] = "true",
                    ["ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds"] = "0"
                });

        Assert.Contains(
            "ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds must be greater than zero",
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a test application factory with the supplied in-memory configuration overrides.
    /// </summary>
    /// <param name="configurationValues">The configuration key/value pairs used to override application settings for a test.</param>
    /// <returns>A configured <see cref="ApplicationWebApplicationFactory"/> instance.</returns>
    /// <summary>
    /// Verifies that IPv6 clients within one /64 share a partition so rotating addresses cannot bypass the limiter.
    /// </summary>
    [Fact]
    public void GetAddressPartitionKey_IPv6AddressesInSameSlash64_ShareOnePartition()
    {
        string first = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:2:aaaa::1"),
            64);
        string second = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:2:bbbb:cccc:dddd:2"),
            64);

        Assert.Equal("2001:db8:1:2::/64", first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// Verifies that IPv6 clients in different /64 networks receive different partitions.
    /// </summary>
    [Fact]
    public void GetAddressPartitionKey_IPv6AddressesInDifferentSlash64_UseDifferentPartitions()
    {
        string first = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:2::1"),
            64);
        string second = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:3::1"),
            64);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Verifies that a prefix length that is not a multiple of eight masks the partial byte correctly.
    /// </summary>
    [Fact]
    public void GetAddressPartitionKey_NonByteAlignedPrefix_MasksPartialByte()
    {
        string partitionKey = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:2f::1"),
            60);

        Assert.Equal("2001:db8:1:20::/60", partitionKey);
    }

    /// <summary>
    /// Verifies that a prefix length of 128 keeps the full IPv6 address.
    /// </summary>
    [Fact]
    public void GetAddressPartitionKey_PrefixLength128_UsesFullAddress()
    {
        string partitionKey = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("2001:db8:1:2:aaaa::1"),
            128);

        Assert.Equal("2001:db8:1:2:aaaa::1", partitionKey);
    }

    /// <summary>
    /// Verifies that IPv4-mapped IPv6 addresses are partitioned by IPv4 address rather than collapsed into one
    /// IPv6 prefix, since a dual-stack listener reports every IPv4 client in that form.
    /// </summary>
    [Fact]
    public void GetAddressPartitionKey_IPv4MappedAddress_UsesIPv4Address()
    {
        string first = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("::ffff:203.0.113.10"),
            64);
        string second = RateLimitingServiceExtensions.GetAddressPartitionKey(
            IPAddress.Parse("::ffff:203.0.113.11"),
            64);

        Assert.Equal("203.0.113.10", first);
        Assert.Equal("203.0.113.11", second);
    }

    /// <summary>
    /// Verifies that client partitioning applies the configured IPv6 prefix length to the remote address.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_IPv6RemoteAddress_UsesConfiguredPrefix()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("2001:db8:1:2:aaaa::1");
        TestLogger logger = new();

        string partitionKey = RateLimitingServiceExtensions.GetClientPartitionKey(
            httpContext,
            new ApplicationRateLimitingOptions(),
            logger);

        Assert.Equal("2001:db8:1:2::/64", partitionKey);
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Verifies that the concurrency policy partitions by endpoint and client by default.
    /// </summary>
    [Fact]
    public void GetConcurrencyPartitionKey_Default_CombinesEndpointAndClient()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/orders";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        string partitionKey = RateLimitingServiceExtensions.GetConcurrencyPartitionKey(
            httpContext,
            new ApplicationRateLimitingOptions(),
            new TestLogger());

        Assert.Equal("/orders|203.0.113.10", partitionKey);
    }

    /// <summary>
    /// Verifies that disabling client partitioning restores one shared permit pool per endpoint.
    /// </summary>
    [Fact]
    public void GetConcurrencyPartitionKey_PartitionByClientDisabled_UsesEndpointOnly()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/orders";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        ApplicationRateLimitingOptions options = new();
        options.ConcurrencyPolicy.PartitionByClient = false;

        string partitionKey = RateLimitingServiceExtensions.GetConcurrencyPartitionKey(
            httpContext,
            options,
            new TestLogger());

        Assert.Equal("/orders", partitionKey);
    }

    /// <summary>
    /// Verifies that startup validation rejects an IPv6 partition prefix length outside 1 through 128.
    /// </summary>
    /// <param name="prefixLength">The configured prefix length.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("129")]
    public void RateLimiting_InvalidIPv6PartitionPrefixLength_FailsStartup(string prefixLength)
    {
        OptionsValidationException exception =
            AssertRateLimitingOptionsValidationFails(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:RateLimiting:Enabled"] = "true",
                    ["ProjectTemplate:RateLimiting:IPv6PartitionPrefixLength"] = prefixLength
                });

        Assert.Contains(
            "ProjectTemplate:RateLimiting:IPv6PartitionPrefixLength must be between 1 and 128",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static ApplicationWebApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?> configurationValues)
    {
        return ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess(configurationValues);
    }

    private static OptionsValidationException AssertRateLimitingOptionsValidationFails(
        IReadOnlyDictionary<string, string?> configurationValues)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        IServiceCollection services = new ServiceCollection();

        _ = services.AddApplicationRateLimiting(
            configuration,
            new TestHostEnvironment());

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        return Assert.Throws<OptionsValidationException>(() =>
            provider
                .GetRequiredService<IOptions<ApplicationRateLimitingOptions>>()
                .Value);
    }

    private static ApplicationWebApplicationFactory CreateSinglePermitGlobalLimiterFactory()
    {
        return CreateFactory(new Dictionary<string, string?>
        {
            ["ProjectTemplate:RateLimiting:Enabled"] = "true",
            ["ProjectTemplate:RateLimiting:UseGlobalLimiter"] = "true",
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "true",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit"] = "1",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds"] = "60",
            ["ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit"] = "0"
        });
    }

    private static async Task<HttpResponseMessage> SendWithAcceptAsync(HttpClient client, string mediaType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static ServiceProvider CreateRequestLoggingServices(ApplicationRequestLoggingOptions requestLoggingOptions)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton(Microsoft.Extensions.Options.Options.Create(requestLoggingOptions));

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateRejectedHttpContext(IServiceProvider services)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
            TraceIdentifier = "trace-rejected"
        };
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/orders";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");

        return httpContext;
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(
                logLevel,
                eventId,
                formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class ManualTimestampProvider : TimeProvider
    {
        private long _timestamp = 1_000_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return _timestamp;
        }

        public void Advance(TimeSpan duration)
        {
            _timestamp += duration.Ticks;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
