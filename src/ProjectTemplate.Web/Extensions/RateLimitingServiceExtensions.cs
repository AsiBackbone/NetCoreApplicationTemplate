using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Constants;
using ProjectTemplate.Web.Diagnostics;
using ProjectTemplate.Web.ErrorHandling;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods to register rate limiting services for the application.
/// </summary>
public static partial class RateLimitingServiceExtensions
{
    internal const string RejectionTitle = "Too Many Requests";
    internal const string RejectionDetail = "Too many requests were received. Please try again later.";
    internal const string RejectionProblemType = "https://www.rfc-editor.org/rfc/rfc6585#section-4";

    private const int _ipv6AddressBitCount = 128;

    /// <summary>
    /// Adds the application's predefined rate limiting policies to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the rate limiting services to.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddApplicationRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        RateLimitingFallbackWarningThrottle fallbackWarningThrottle = new(
            TimeProvider.System,
            RateLimitingFallbackWarningThrottle.DefaultInterval);

        services.Configure<ApplicationRateLimitingOptions>(options =>
        {
            ApplicationRateLimitingOptions defaultOptions = CreateDefaultOptions(environment);

            options.Enabled = defaultOptions.Enabled;
            options.UseGlobalLimiter = defaultOptions.UseGlobalLimiter;
            options.UseSharedUnknownClientPartition = defaultOptions.UseSharedUnknownClientPartition;
            options.UnknownClientPartitionKey = defaultOptions.UnknownClientPartitionKey;

            options.GlobalFixedWindow.PermitLimit = defaultOptions.GlobalFixedWindow.PermitLimit;
            options.GlobalFixedWindow.WindowSeconds = defaultOptions.GlobalFixedWindow.WindowSeconds;
            options.GlobalFixedWindow.QueueLimit = defaultOptions.GlobalFixedWindow.QueueLimit;

            options.FixedWindowPolicy.PermitLimit = defaultOptions.FixedWindowPolicy.PermitLimit;
            options.FixedWindowPolicy.WindowSeconds = defaultOptions.FixedWindowPolicy.WindowSeconds;
            options.FixedWindowPolicy.QueueLimit = defaultOptions.FixedWindowPolicy.QueueLimit;

            options.ConcurrencyPolicy.PermitLimit = defaultOptions.ConcurrencyPolicy.PermitLimit;
            options.ConcurrencyPolicy.QueueLimit = defaultOptions.ConcurrencyPolicy.QueueLimit;
        });

        services
            .AddOptions<ApplicationRateLimitingOptions>()
            .Bind(configuration.GetSection(ApplicationRateLimitingOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.UnknownClientPartitionKey),
                "ProjectTemplate:RateLimiting:UnknownClientPartitionKey must not be empty.")
            .Validate(options => options.IPv6PartitionPrefixLength is >= 1 and <= _ipv6AddressBitCount,
                "ProjectTemplate:RateLimiting:IPv6PartitionPrefixLength must be between 1 and 128.")
            .Validate(options => options.GlobalFixedWindow.PermitLimit > 0,
                "ProjectTemplate:RateLimiting:GlobalFixedWindow:PermitLimit must be greater than zero.")
            .Validate(options => options.GlobalFixedWindow.WindowSeconds > 0,
                "ProjectTemplate:RateLimiting:GlobalFixedWindow:WindowSeconds must be greater than zero.")
            .Validate(options => options.GlobalFixedWindow.QueueLimit >= 0,
                "ProjectTemplate:RateLimiting:GlobalFixedWindow:QueueLimit must be zero or greater.")
            .Validate(options => options.FixedWindowPolicy.PermitLimit > 0,
                "ProjectTemplate:RateLimiting:FixedWindowPolicy:PermitLimit must be greater than zero.")
            .Validate(options => options.FixedWindowPolicy.WindowSeconds > 0,
                "ProjectTemplate:RateLimiting:FixedWindowPolicy:WindowSeconds must be greater than zero.")
            .Validate(options => options.FixedWindowPolicy.QueueLimit >= 0,
                "ProjectTemplate:RateLimiting:FixedWindowPolicy:QueueLimit must be zero or greater.")
            .Validate(options => options.ConcurrencyPolicy.PermitLimit > 0,
                "ProjectTemplate:RateLimiting:ConcurrencyPolicy:PermitLimit must be greater than zero.")
            .Validate(options => options.ConcurrencyPolicy.QueueLimit >= 0,
                "ProjectTemplate:RateLimiting:ConcurrencyPolicy:QueueLimit must be zero or greater.")
            .ValidateOnStart();

        _ = services.AddRateLimiter();

        _ = services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<ApplicationRateLimitingOptions>>((options, rateLimitingOptionsAccessor) =>
            {
                ApplicationRateLimitingOptions rateLimitingOptions = rateLimitingOptionsAccessor.Value;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, cancellationToken) =>
                {
                    HttpContext httpContext = context.HttpContext;

                    TimeSpan? retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfterValue)
                        ? retryAfterValue
                        : null;

                    ILogger logger = httpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Template.Web.RateLimiting");

                    LogRejectedRequest(httpContext, logger, retryAfter);

                    await WriteRejectionResponseAsync(httpContext, retryAfter, cancellationToken)
                        .ConfigureAwait(false);
                };

                if (!rateLimitingOptions.Enabled)
                {
                    return;
                }

                if (rateLimitingOptions.UseGlobalLimiter)
                {
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetClientPartitionKey(httpContext, rateLimitingOptions, fallbackWarningThrottle),
                            factory: _ => CreateFixedWindowRateLimiterOptions(rateLimitingOptions.GlobalFixedWindow)));
                }

                options.AddPolicy(ApplicationRateLimitingPolicyNames.Fixed, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetClientPartitionKey(httpContext, rateLimitingOptions, fallbackWarningThrottle),
                        factory: _ => CreateFixedWindowRateLimiterOptions(rateLimitingOptions.FixedWindowPolicy)));

                options.AddPolicy(ApplicationRateLimitingPolicyNames.Concurrency, httpContext =>
                    RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: GetConcurrencyPartitionKey(
                            httpContext,
                            rateLimitingOptions,
                            CreateRateLimitingLogger(httpContext),
                            fallbackWarningThrottle),
                        factory: _ => CreateConcurrencyLimiterOptions(rateLimitingOptions.ConcurrencyPolicy)));
            });

        return services;
    }
    /// <summary>
    /// Writes the rejection log entry for a rate-limited request, honoring the request-logging privacy options.
    /// </summary>
    /// <param name="httpContext">The rejected request's HTTP context.</param>
    /// <param name="logger">The logger that receives the entry.</param>
    /// <param name="retryAfter">The retry interval reported by the limiter lease, when available.</param>
    internal static void LogRejectedRequest(
        HttpContext httpContext,
        ILogger logger,
        TimeSpan? retryAfter)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(logger);

        LogRateLimitRejectedRequest(
            logger,
            httpContext.Request.Method,
            httpContext.Request.Path.Value ?? string.Empty,
            RequestLoggingPrivacy.GetLoggableRemoteIpAddress(httpContext),
            httpContext.GetEndpoint()?.DisplayName,
            retryAfter?.TotalSeconds,
            httpContext.TraceIdentifier);
    }

    /// <summary>
    /// Writes the <c>429 Too Many Requests</c> response for a rate-limited request.
    /// </summary>
    /// <remarks>
    /// API-shaped requests, as classified by <see cref="ProblemDetailsRequestClassifier"/>, receive a Problem Details
    /// response through <see cref="IProblemDetailsService"/>, so the shared customization adds the trace, request, and
    /// correlation identifiers. Other requests receive a short plain-text body. The browser error page is intentionally
    /// not re-executed: rejections must stay cheap, and rendering a view for every rejected request would work against
    /// the protection the limiter provides.
    /// </remarks>
    /// <param name="httpContext">The rejected request's HTTP context.</param>
    /// <param name="retryAfter">The retry interval reported by the limiter lease, when available.</param>
    /// <param name="cancellationToken">A token that cancels writing the response.</param>
    /// <returns>A task that completes when the response has been written.</returns>
    internal static async Task WriteRejectionResponseAsync(
        HttpContext httpContext,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        HttpResponse response = httpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (retryAfter is TimeSpan retryAfterValue)
        {
            response.Headers.RetryAfter = Math.Ceiling(retryAfterValue.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        if (ProblemDetailsRequestClassifier.ShouldWriteProblemDetails(httpContext))
        {
            IProblemDetailsService? problemDetailsService = httpContext.RequestServices?
                .GetService<IProblemDetailsService>();

            if (problemDetailsService is not null)
            {
                ProblemDetailsContext problemDetailsContext = new()
                {
                    HttpContext = httpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = RejectionTitle,
                        Type = RejectionProblemType,
                        Detail = RejectionDetail
                    }
                };

                if (await problemDetailsService.TryWriteAsync(problemDetailsContext).ConfigureAwait(false))
                {
                    return;
                }
            }
        }

        if (HttpMethods.IsHead(httpContext.Request.Method))
        {
            return;
        }

        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync(RejectionDetail, cancellationToken).ConfigureAwait(false);
    }

    private static ApplicationRateLimitingOptions CreateDefaultOptions(IHostEnvironment environment)
    {
        ApplicationRateLimitingOptions options = new();

        if (environment.IsDevelopment())
        {
            options.GlobalFixedWindow.PermitLimit = 300;
            options.GlobalFixedWindow.WindowSeconds = 60;

            options.FixedWindowPolicy.PermitLimit = 120;
            options.FixedWindowPolicy.WindowSeconds = 60;

            options.ConcurrencyPolicy.PermitLimit = 20;
        }

        return options;
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowRateLimiterOptions(
        FixedWindowRateLimitingOptions options)
    {
        return new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = EnsureAtLeast(options.PermitLimit, minimum: 1),
            Window = TimeSpan.FromSeconds(EnsureAtLeast(options.WindowSeconds, minimum: 1)),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = EnsureAtLeast(options.QueueLimit, minimum: 0)
        };
    }

    private static ConcurrencyLimiterOptions CreateConcurrencyLimiterOptions(
        ConcurrencyRateLimitingOptions options)
    {
        return new ConcurrencyLimiterOptions
        {
            PermitLimit = EnsureAtLeast(options.PermitLimit, minimum: 1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = EnsureAtLeast(options.QueueLimit, minimum: 0)
        };
    }

    private static string GetClientPartitionKey(
        HttpContext httpContext,
        ApplicationRateLimitingOptions options,
        RateLimitingFallbackWarningThrottle fallbackWarningThrottle)
    {
        return GetClientPartitionKey(
            httpContext,
            options,
            CreateRateLimitingLogger(httpContext),
            fallbackWarningThrottle);
    }

    private static ILogger CreateRateLimitingLogger(HttpContext httpContext)
    {
        return httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Template.Web.RateLimiting");
    }

    internal static string GetClientPartitionKey(
        HttpContext httpContext,
        ApplicationRateLimitingOptions options,
        ILogger logger,
        RateLimitingFallbackWarningThrottle? fallbackWarningThrottle = null)
    {
        IPAddress? remoteIpAddress = httpContext.Connection.RemoteIpAddress;

        if (remoteIpAddress is not null)
        {
            return GetAddressPartitionKey(remoteIpAddress, options.IPv6PartitionPrefixLength);
        }

        string fallbackPartitionKey = string.IsNullOrWhiteSpace(options.UnknownClientPartitionKey)
            ? "unknown-client"
            : options.UnknownClientPartitionKey.Trim();
        string fallbackMode = options.UseSharedUnknownClientPartition ? "Shared" : "PerRequest";
        string fallbackDiscriminator = string.IsNullOrWhiteSpace(httpContext.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : httpContext.TraceIdentifier;

        if (!options.UseSharedUnknownClientPartition)
        {
            fallbackPartitionKey = $"{fallbackPartitionKey}:{fallbackDiscriminator}";
        }

        long suppressedWarningCount = 0;

        if (fallbackWarningThrottle is null ||
            fallbackWarningThrottle.TryAcquire(out suppressedWarningCount))
        {
            LogRateLimitingClientPartitionFallback(
                logger,
                fallbackMode,
                fallbackPartitionKey,
                fallbackDiscriminator,
                suppressedWarningCount);
        }

        return fallbackPartitionKey;
    }

    /// <summary>
    /// Returns the rate limiting partition key for a client address.
    /// </summary>
    /// <remarks>
    /// IPv4-mapped IPv6 addresses are converted to IPv4 first, because a dual-stack listener reports IPv4 clients in
    /// that form and masking them as IPv6 would place every IPv4 client in one partition. Other IPv6 addresses are
    /// reduced to their network prefix so a client cannot bypass the limiter by rotating addresses inside it.
    /// </remarks>
    /// <param name="address">The client address.</param>
    /// <param name="ipv6PrefixLength">The IPv6 prefix length that identifies one client.</param>
    /// <returns>The partition key.</returns>
    internal static string GetAddressPartitionKey(IPAddress address, int ipv6PrefixLength)
    {
        ArgumentNullException.ThrowIfNull(address);

        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (normalizedAddress.AddressFamily != AddressFamily.InterNetworkV6
            || ipv6PrefixLength >= _ipv6AddressBitCount)
        {
            return normalizedAddress.ToString();
        }

        int prefixLength = Math.Max(ipv6PrefixLength, 1);
        byte[] addressBytes = normalizedAddress.GetAddressBytes();
        for (int bitIndex = prefixLength; bitIndex < _ipv6AddressBitCount; bitIndex++)
        {
            addressBytes[bitIndex / 8] &= (byte)~(0x80 >> (bitIndex % 8));
        }

        return string.Create(CultureInfo.InvariantCulture, $"{new IPAddress(addressBytes)}/{prefixLength}");
    }

    /// <summary>
    /// Returns the partition key used by the named concurrency policy.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="options">The rate limiting options.</param>
    /// <param name="logger">The logger used when the client address falls back to an unknown-client partition.</param>
    /// <param name="fallbackWarningThrottle">An optional throttle for fallback warnings.</param>
    /// <returns>The endpoint key, combined with the client key when concurrency is partitioned by client.</returns>
    internal static string GetConcurrencyPartitionKey(
        HttpContext httpContext,
        ApplicationRateLimitingOptions options,
        ILogger logger,
        RateLimitingFallbackWarningThrottle? fallbackWarningThrottle = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(options);

        string endpointPartitionKey = GetEndpointPartitionKey(httpContext);

        return options.ConcurrencyPolicy.PartitionByClient
            ? $"{endpointPartitionKey}|{GetClientPartitionKey(httpContext, options, logger, fallbackWarningThrottle)}"
            : endpointPartitionKey;
    }

    private static string GetEndpointPartitionKey(HttpContext httpContext)
    {
        return httpContext.GetEndpoint()?.DisplayName
            ?? httpContext.Request.Path.Value
            ?? "unknown-endpoint";
    }

    private static int EnsureAtLeast(int value, int minimum)
    {
        return value < minimum ? minimum : value;
    }

    [LoggerMessage(
        EventId = ApplicationLogEventIds.RateLimitRejectedRequest,
        Level = LogLevel.Warning,
        Message = "Rate limit rejected request. Method: {Method}; Path: {Path}; RemoteIpAddress: {RemoteIpAddress}; Endpoint: {Endpoint}; RetryAfterSeconds: {RetryAfterSeconds}; TraceIdentifier: {TraceIdentifier}")]
    private static partial void LogRateLimitRejectedRequest(
        ILogger logger,
        string method,
        string path,
        string? remoteIpAddress,
        string? endpoint,
        double? retryAfterSeconds,
        string traceIdentifier);

    [LoggerMessage(
        EventId = ApplicationLogEventIds.RateLimitClientPartitionFallback,
        Level = LogLevel.Warning,
        Message = "Rate limiting used fallback client partition because RemoteIpAddress was unavailable. FallbackMode: {FallbackMode}; PartitionKey: {PartitionKey}; TraceIdentifier: {TraceIdentifier}; SuppressedWarningCount: {SuppressedWarningCount}. Verify forwarded headers and trusted proxy configuration when running behind a proxy or load balancer.")]
    private static partial void LogRateLimitingClientPartitionFallback(
        ILogger logger,
        string fallbackMode,
        string partitionKey,
        string traceIdentifier,
        long suppressedWarningCount);
}
