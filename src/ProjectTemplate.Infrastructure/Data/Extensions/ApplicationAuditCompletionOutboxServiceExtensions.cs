using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Infrastructure.Data.Extensions;

/// <summary>
/// Provides opt-in registration for durable audit-completion outbox services.
/// </summary>
public static class ApplicationAuditCompletionOutboxServiceExtensions
{
    /// <summary>
    /// Registers durable staging, dispatch, and minimized operational query services.
    /// </summary>
    /// <remarks>
    /// Call <see cref="InfrastructureDataAccessServiceExtensions" /> before this method.
    /// This infrastructure registration does not start a background dispatcher loop.
    /// </remarks>
    public static IServiceCollection AddApplicationAuditCompletionOutboxCore(
        this IServiceCollection services,
        Action<ApplicationAuditCompletionOutboxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<ApplicationAuditCompletionOutboxOptions> optionsBuilder = services
            .AddOptions<ApplicationAuditCompletionOutboxOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.DefaultDestination),
                "The default audit-completion destination must not be empty.")
            .Validate(options => options.DefaultDestination.Length <= 128,
                "The default audit-completion destination cannot exceed 128 characters.")
            .Validate(options => options.BatchSize > 0,
                "The audit-completion outbox batch size must be greater than zero.")
            .Validate(options => options.PollInterval > TimeSpan.Zero,
                "The audit-completion polling interval must be greater than zero.")
            .Validate(options => options.MaximumCycleRetryDelay >= options.PollInterval,
                "The maximum audit-completion cycle retry delay must not be less than the polling interval.")
            .Validate(options => options.BaseRetryDelay > TimeSpan.Zero,
                "The base audit-completion retry delay must be greater than zero.")
            .Validate(options => options.MaxRetryDelay >= options.BaseRetryDelay,
                "The maximum audit-completion retry delay must not be less than the base delay.")
            .Validate(options => options.DeferredRetryDelay > TimeSpan.Zero,
                "The deferred audit-completion retry delay must be greater than zero.")
            .Validate(options => options.MaxRetryAttempts > 0,
                "The maximum audit-completion retry attempts must be greater than zero.")
            .Validate(options => options.MaxErrorDetailLength is > 0 and <= 512,
                "The audit-completion error detail length must be between 1 and 512 characters.")
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ApplicationAuditCompletionOutbox>();
        services.TryAddScoped<IApplicationAuditCompletionOutbox>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationAuditCompletionOutbox>());
        services.TryAddScoped<IApplicationAuditCompletionOutboxDispatcher>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationAuditCompletionOutbox>());
        services.TryAddScoped<IApplicationAuditCompletionOutboxQuery>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationAuditCompletionOutbox>());

        return services;
    }
}
