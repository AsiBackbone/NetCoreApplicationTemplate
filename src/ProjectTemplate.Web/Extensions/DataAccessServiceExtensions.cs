using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Extensions;
using ProjectTemplate.Infrastructure.Data.Services;
using ProjectTemplate.Web.Accessors;
using ProjectTemplate.Web.HealthChecks;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides service registration methods for application data access.
/// </summary>
public static class DataAccessServiceExtensions
{
    /// <summary>
    /// Registers EF Core data access services for the web application.
    /// </summary>
    public static IServiceCollection AddApplicationDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActorAccessor, HttpContextCurrentActorAccessor>();
        services.AddScoped<IApplicationAuditContextAccessor, HttpContextApplicationAuditContextAccessor>();

        services.AddApplicationInfrastructureDataAccess(configuration);
        AddApplicationDatabaseReadinessCheck(services, configuration);

        services.AddHostedService<DataAccessStartupLogger>();

        return services;
    }

    private static void AddApplicationDatabaseReadinessCheck(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ApplicationHealthCheckOptions healthCheckOptions = configuration
            .GetSection(ApplicationHealthCheckOptions.SectionName)
            .Get<ApplicationHealthCheckOptions>() ?? new ApplicationHealthCheckOptions();

        // Infrastructure registers ApplicationDbContext only when a data access provider is enabled.
        bool dataAccessEnabled = services.Any(descriptor => descriptor.ServiceType == typeof(ApplicationDbContext));

        if (!healthCheckOptions.DatabaseReadinessCheckEnabled || !dataAccessEnabled)
        {
            return;
        }

        services.AddHealthChecks()
            .AddCheck<ApplicationDatabaseHealthCheck>(
                "application-database",
                tags: [ApplicationHealthCheckTags.Ready, ApplicationHealthCheckTags.Database]);
    }
}
