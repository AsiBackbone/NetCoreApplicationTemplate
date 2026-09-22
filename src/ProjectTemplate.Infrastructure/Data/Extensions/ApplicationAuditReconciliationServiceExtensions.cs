using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Infrastructure.Data.Extensions;

public static class ApplicationAuditReconciliationServiceExtensions
{
    public static IServiceCollection AddApplicationAuditReconciliationCore(
        this IServiceCollection services,
        Action<ApplicationAuditReconciliationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<ApplicationAuditReconciliationOptions> optionsBuilder = services
            .AddOptions<ApplicationAuditReconciliationOptions>()
            .Validate(options => options.Interval > TimeSpan.Zero,
                "The audit reconciliation interval must be greater than zero.")
            .Validate(options => options.MaximumCycleRetryDelay >= options.Interval,
                "The maximum audit reconciliation cycle retry delay must not be less than the interval.")
            .Validate(options => options.CompletionGracePeriod >= TimeSpan.Zero,
                "The completion grace period must not be negative.")
            .Validate(options => options.StalePendingThreshold > TimeSpan.Zero,
                "The stale pending threshold must be greater than zero.")
            .Validate(options => options.StaleRetryReadyThreshold > TimeSpan.Zero,
                "The stale retry-ready threshold must be greater than zero.")
            .Validate(options => options.MaximumBatchesPerRun is > 0 and <= 10_000,
                "Maximum batches per reconciliation run must be between 1 and 10000.")
            .Validate(options => options.MaximumMalformedRecordsPerRun is > 0 and <= 10_000,
                "Maximum malformed audit records per reconciliation run must be between 1 and 10000.")
            .Validate(options => options.HealthWarningFindingCount >= 0,
                "The warning finding threshold must not be negative.")
            .Validate(options => options.HealthUnhealthyFindingCount >= options.HealthWarningFindingCount,
                "The unhealthy finding threshold must not be less than the warning threshold.")
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ApplicationAuditReconciliationMetrics>();
        services.TryAddScoped<ApplicationAuditReconciler>();
        services.TryAddScoped<IApplicationAuditReconciler>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationAuditReconciler>());
        return services;
    }
}
