namespace ProjectTemplate.Web.Options;

/// <summary>
/// Represents template-level API versioning configuration.
/// </summary>
public sealed class ApplicationApiVersioningOptions
{
    /// <summary>
    /// Configuration section name for API versioning settings.
    /// </summary>
    public const string SectionName = "ProjectTemplate:ApiVersioning";

    /// <summary>
    /// Gets or sets the default major API version.
    /// </summary>
    public int DefaultMajorVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the default minor API version.
    /// </summary>
    public int DefaultMinorVersion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an unspecified API version should use the default version.
    /// </summary>
    public bool AssumeDefaultVersionWhenUnspecified { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether supported and deprecated API versions should be reported in response headers.
    /// </summary>
    public bool ReportApiVersions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether URL segment versioning is enabled.
    /// </summary>
    public bool EnableUrlSegmentVersioning { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether header-based versioning is enabled.
    /// </summary>
    public bool EnableHeaderVersioning { get; set; } = true;

    /// <summary>
    /// Gets or sets the request header name used for header-based API versioning.
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Version";

    /// <summary>
    /// Gets or sets the date a deprecated API version stops being served, advertised as the <c>Sunset</c> header.
    /// </summary>
    /// <remarks>
    /// Unset by default, and the header is omitted when it is unset. A sunset date is a commitment to the callers of
    /// a specific deployment about when a version disappears, so a template cannot supply a meaningful one: any date
    /// shipped here is either arbitrary or, once it passes, an advertised removal that already happened. Set this when
    /// the application actually deprecates a version, to a date in the future.
    /// </remarks>
    public DateTimeOffset? DeprecatedVersionSunset { get; set; }
}
