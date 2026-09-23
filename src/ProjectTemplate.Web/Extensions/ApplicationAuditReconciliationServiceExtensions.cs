using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Extensions;
using ProjectTemplate.Web.HealthChecks;
using ProjectTemplate.Web.Services;

namespace ProjectTemplate.Web.Extensions;

public static class ApplicationAuditReconciliationServiceExtensions
{
    public static IServiceCollection AddApplicationAuditReconciliation(
        this IServiceCollection services,
        Action<ApplicationAuditReconciliationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApplicationAuditReconciliationCore(configure);
        services.AddHostedService<ApplicationAuditReconciliationHostedService>();
        services.AddHealthChecks()
            .AddCheck<ApplicationAuditIntegrityHealthCheck>(
                "application-audit-integrity",
                tags: [ApplicationHealthCheckTags.Audit, ApplicationHealthCheckTags.Integrity]);
        return services;
    }
}
