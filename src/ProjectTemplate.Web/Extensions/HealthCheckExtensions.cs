using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ProjectTemplate.Web.HealthChecks;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods for registering and mapping application health check endpoints.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Registers baseline ASP.NET Core health check services.
    /// </summary>
    /// <param name="services">The service collection used to register health checks.</param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();

        return services;
    }

    /// <summary>
    /// Maps baseline health check endpoints for infrastructure, reverse proxies, and hosting platforms.
    /// </summary>
    /// <remarks>
    /// <c>/health/ready</c> runs only checks tagged <see cref="ApplicationHealthCheckTags.Ready"/>, and
    /// <c>/health/audit-integrity</c> runs only checks tagged <see cref="ApplicationHealthCheckTags.Audit"/>. Audit integrity is
    /// reported separately so an integrity finding alerts operators without removing every replica from rotation.
    /// </remarks>
    /// <param name="app">The web application used to map health check endpoints.</param>
    /// <returns>The original <see cref="WebApplication"/> for chaining.</returns>
    public static WebApplication MapApplicationHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health")
            .AllowAnonymous();

        app.MapHealthChecks("/health/audit-integrity", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains(ApplicationHealthCheckTags.Ready)
        })
        .AllowAnonymous();

        app.MapHealthChecks("/health/audit", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains(ApplicationHealthCheckTags.Audit)
        })
        .AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        })
        .AllowAnonymous();

        return app;
    }
}
