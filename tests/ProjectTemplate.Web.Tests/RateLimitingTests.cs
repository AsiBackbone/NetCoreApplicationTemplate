using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    /// Verifies that rejected requests return the expected JSON 429 Too Many Requests response payload.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RejectedRequest_ReturnsJsonTooManyRequestsResponseShape()
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
        using HttpResponseMessage rejectedResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        Assert.Equal("application/json", rejectedResponse.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await rejectedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Too many requests.", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(429, document.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
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
            ["ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition"] = "true",
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
        Assert.True(options.UseSharedUnknownClientPartition);
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
    /// Verifies that unresolved client IP addresses use a per-request fallback partition by default.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_UsesPerRequestFallback_WhenRemoteIpAddressIsUnavailableByDefault()
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

        Assert.Equal("unknown-client:trace-331", partitionKey);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(6002, entry.EventId.Id);
        Assert.Contains("RemoteIpAddress was unavailable", entry.Message, StringComparison.Ordinal);
        Assert.Contains("PerRequest", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that unresolved client IP addresses only share a fallback partition when explicitly configured.
    /// </summary>
    [Fact]
    public void GetClientPartitionKey_UsesSharedFallback_WhenExplicitlyConfigured()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-331-shared"
        };
        TestLogger logger = new();
        ApplicationRateLimitingOptions options = new()
        {
            UseSharedUnknownClientPartition = true,
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
        Assert.Contains("Shared", entry.Message, StringComparison.Ordinal);
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
