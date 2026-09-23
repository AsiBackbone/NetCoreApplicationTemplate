namespace ProjectTemplate.Web.Options;

/// <summary>
/// Represents application health check configuration.
/// </summary>
public sealed class ApplicationHealthCheckOptions
{
    /// <summary>
    /// Configuration section name for health check settings.
    /// </summary>
    public const string SectionName = "ProjectTemplate:HealthChecks";

    /// <summary>
    /// Gets or sets a value indicating whether <c>/health/ready</c> includes an application database connectivity
    /// check when EF Core data access is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/> so readiness reflects whether the instance can reach the database it needs to
    /// serve traffic. The check is not registered when the data access provider is <c>None</c>. The value is read each
    /// time the check runs; when it is <see langword="false"/>, the registered check reports healthy without
    /// contacting the database.
    /// </remarks>
    public bool DatabaseReadinessCheckEnabled { get; set; } = true;
}
