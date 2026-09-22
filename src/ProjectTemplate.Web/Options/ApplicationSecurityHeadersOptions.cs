namespace ProjectTemplate.Web.Options;

/// <summary>
/// Options to control which security-related HTTP headers are applied by the application.
/// </summary>
public sealed class ApplicationSecurityHeadersOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind security header settings.
    /// </summary>
    public const string SectionName = "ProjectTemplate:SecurityHeaders";

    /// <summary>
    /// Gets or sets a value indicating whether security headers are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Content-Security-Policy header is applied.
    /// </summary>
    public bool EnableContentSecurityPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Permissions-Policy header is applied.
    /// </summary>
    public bool EnablePermissionsPolicy { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cross-origin related headers are applied.
    /// </summary>
    public bool EnableCrossOriginHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets the Content-Security-Policy header value applied to responses.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "img-src 'self' data:; " +
        "script-src 'self'; " +
        "style-src 'self';";

    /// <summary>
    /// Gets or sets the Permissions-Policy header value applied to responses.
    /// Set EnablePermissionsPolicy to false to omit the header.
    /// </summary>
    public string PermissionsPolicy { get; set; } =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)";

    /// <summary>
    /// Gets or sets path prefixes that are excluded from applying the security headers.
    /// </summary>
    public List<string> ExcludedPathPrefixes { get; set; } =
    [
            "/health",
            "/metrics"
    ];
}
