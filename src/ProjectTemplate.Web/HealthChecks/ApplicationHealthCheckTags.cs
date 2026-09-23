namespace ProjectTemplate.Web.HealthChecks;

/// <summary>
/// Defines the health check tags that select which checks each health endpoint runs.
/// </summary>
public static class ApplicationHealthCheckTags
{
    /// <summary>
    /// Selects checks for <c>/health/ready</c>. Use it only for dependencies without which this instance cannot serve
    /// normal traffic, because a failing readiness check removes the instance from load balancing.
    /// </summary>
    public const string Ready = "ready";

    /// <summary>
    /// Selects checks for <c>/health/audit-integrity</c>. Audit integrity and delivery state require operator attention, but
    /// they do not determine whether an instance can serve traffic, so these checks are kept out of readiness.
    /// </summary>
    public const string Audit = "audit";

    /// <summary>
    /// Identifies checks that evaluate audit integrity.
    /// </summary>
    public const string Integrity = "integrity";

    /// <summary>
    /// Identifies checks that evaluate application database connectivity.
    /// </summary>
    public const string Database = "database";
}
