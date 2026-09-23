namespace ProjectTemplate.Web.Options;

/// <summary>
/// Represents template-level rate limiting configuration.
/// </summary>
public sealed class ApplicationRateLimitingOptions
{
    /// <summary>
    /// Configuration section name for rate limiting settings.
    /// </summary>
    public const string SectionName = "ProjectTemplate:RateLimiting";

    /// <summary>
    /// Gets or sets a value indicating whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to use the global limiter.
    /// When true, <see cref="GlobalFixedWindow"/> is used as a global limiter.
    /// </summary>
    public bool UseGlobalLimiter { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether requests without a resolved client IP address
    /// share one fallback partition key.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> so unresolved clients remain rate limited. Setting this to
    /// <see langword="false"/> gives each unresolved request its own partition, which effectively disables
    /// client rate limiting for those requests.
    /// </remarks>
    public bool UseSharedUnknownClientPartition { get; set; } = true;

    /// <summary>
    /// Gets or sets the base fallback partition key used when the client IP address cannot be resolved.
    /// </summary>
    public string UnknownClientPartitionKey { get; set; } = "unknown-client";

    /// <summary>
    /// Gets or sets the IPv6 prefix length used to group client addresses into one rate limiting partition.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>64</c>. A single IPv6 subscriber commonly controls a whole /64 and can rotate addresses within it,
    /// so partitioning by full address would let one client bypass per-client limits. Use <c>128</c> to partition by
    /// full address. IPv4 and IPv4-mapped IPv6 addresses are always partitioned by their IPv4 address.
    /// </remarks>
    public int IPv6PartitionPrefixLength { get; set; } = 64;

    /// <summary>
    /// Gets or sets the global fixed-window rate limiting options.
    /// </summary>
    public FixedWindowRateLimitingOptions GlobalFixedWindow { get; set; } = new()
    {
        PermitLimit = 60,
        WindowSeconds = 60,
        QueueLimit = 0
    };

    /// <summary>
    /// Gets or sets the fixed-window rate limiting options applied at the template level.
    /// </summary>
    public FixedWindowRateLimitingOptions FixedWindowPolicy { get; set; } = new()
    {
        PermitLimit = 60,
        WindowSeconds = 60,
        QueueLimit = 0
    };

    /// <summary>
    /// Gets or sets the concurrency-based rate limiting options applied at the template level.
    /// </summary>
    public ConcurrencyRateLimitingOptions ConcurrencyPolicy { get; set; } = new()
    {
        PermitLimit = 10,
        QueueLimit = 0
    };
}
