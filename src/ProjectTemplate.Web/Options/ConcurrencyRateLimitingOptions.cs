namespace ProjectTemplate.Web.Options;

/// <summary>
/// Represents concurrency rate limiting configuration.
/// </summary>
public sealed class ConcurrencyRateLimitingOptions
{
    /// <summary>
    /// The maximum number of concurrent permits allowed.
    /// </summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// The maximum number of requests allowed to wait in the queue.
    /// </summary>
    public int QueueLimit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each client receives its own concurrency permits for an endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>, so one client holding slow requests cannot exhaust an endpoint's permits for
    /// every other client. Set to <see langword="false"/> to share one permit pool per endpoint across all clients,
    /// which caps total endpoint concurrency but lets a single client consume every permit.
    /// </remarks>
    public bool PartitionByClient { get; set; } = true;
}
