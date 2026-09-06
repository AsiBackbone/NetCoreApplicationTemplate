namespace ProjectTemplate.Web.Options;

/// <summary>
/// Options controlling structured HTTP request logging behavior.
/// </summary>
public sealed class ApplicationRequestLoggingOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind request logging settings.
    /// </summary>
    public static string SectionName { get; internal set; } = "ProjectTemplate:RequestLogging";

    /// <summary>
    /// Gets or sets a value indicating whether structured request logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the request/response header used for request correlation.
    /// </summary>
    public string CorrelationHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets a value indicating whether the query string should be logged.
    /// Disabled by default because query strings may contain sensitive values.
    /// </summary>
    public bool IncludeQueryString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether authenticated user names should be logged.
    /// </summary>
    /// <remarks>
    /// Disabled by default. A user name identifies a person, and request logs are typically shipped and retained for
    /// longer than the data they describe. Enable this when the application has decided that request attribution is
    /// needed and its log retention accounts for personal data.
    /// </remarks>
    public bool IncludeUserName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the remote IP address should be logged.
    /// </summary>
    /// <remarks>
    /// Disabled by default for the same reason as <see cref="IncludeUserName" />. A client IP address is treated as
    /// personal data under several privacy regimes. Enable this when incident response or abuse handling needs it,
    /// and note that the value is only meaningful once forwarded headers are configured with trusted proxies.
    /// </remarks>
    public bool IncludeRemoteIpAddress { get; set; }

    /// <summary>
    /// Gets or sets path prefixes that should be excluded from normal request logging.
    /// Matching requests are logged at Verbose level so the default sinks suppress them.
    /// </summary>
    public List<string> ExcludedPathPrefixes { get; set; } =
    [
        "/health",
        "/metrics",
        "/favicon.ico",
        "/css",
        "/js",
        "/lib",
        "/_framework"
    ];
}
