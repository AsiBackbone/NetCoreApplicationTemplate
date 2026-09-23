using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Web.HealthChecks;

/// <summary>
/// Reports audit reconciliation findings, audit delivery state, and whether reconciliation is still running.
/// </summary>
/// <remarks>
/// The check is registered with the <see cref="ApplicationHealthCheckTags.Audit"/> tag and exposed through
/// <c>/health/audit</c>. It is intentionally excluded from readiness: an integrity finding requires operator review,
/// and a readiness failure would remove every replica from load balancing at the same moment.
/// </remarks>
public sealed class ApplicationAuditIntegrityHealthCheck(
    IServiceScopeFactory scopeFactory,
    IOptions<ApplicationAuditReconciliationOptions> options,
    TimeProvider timeProvider)
    : IHealthCheck
{
    internal const string NeverRunDescription =
        "Audit reconciliation has not completed a run in this process.";

    internal const string StaleRunDescription =
        "The last successful audit reconciliation run is older than the configured stale-run threshold.";

    private const int _defaultStaleRunIntervalMultiplier = 3;

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ApplicationAuditReconciliationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// Evaluates audit reconciliation findings, delivery health, and reconciliation freshness.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The audit integrity health result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Audit reconciliation is disabled.");
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        IApplicationAuditReconciler reconciler = scope.ServiceProvider
            .GetRequiredService<IApplicationAuditReconciler>();
        ApplicationAuditReconciliationSummary summary = await reconciler
            .GetSummaryAsync(cancellationToken)
            .ConfigureAwait(false);

        ApplicationAuditCompletionOutboxHealth? deliveryHealth = null;
        IApplicationAuditCompletionOutboxQuery? outboxQuery = scope.ServiceProvider
            .GetService<IApplicationAuditCompletionOutboxQuery>();
        if (outboxQuery is not null)
        {
            deliveryHealth = await outboxQuery.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            scope.ServiceProvider
                .GetRequiredService<ApplicationAuditReconciliationMetrics>()
                .UpdateDelivery(deliveryHealth);
        }

        var data = new Dictionary<string, object>
        {
            ["openFindings"] = summary.OpenFindingCount,
            ["errorFindings"] = summary.ErrorFindingCount,
            ["criticalFindings"] = summary.CriticalFindingCount,
            ["manifestVerificationFailures"] = summary.ManifestVerificationFailureCount,
            ["missingCompletions"] = summary.MissingCompletionCount,
            ["staleDeliveryFindings"] = summary.StaleDeliveryCount,
            ["deadLetterFindings"] = summary.DeadLetterCount
        };

        if (summary.LastRunUtc.HasValue)
        {
            data["lastReconciliationUtc"] = summary.LastRunUtc.Value;
        }

        if (deliveryHealth is not null)
        {
            data["outboxBacklog"] = deliveryHealth.BacklogCount;
            data["outboxRetryCount"] = deliveryHealth.TotalRetryCount;
            data["outboxDeadLetters"] = deliveryHealth.DeadLetterCount;
            if (deliveryHealth.OldestPendingAge.HasValue)
            {
                data["oldestPendingAgeSeconds"] = deliveryHealth.OldestPendingAge.Value.TotalSeconds;
            }
        }

        string? freshnessProblem = EvaluateRunFreshness(summary.LastRunUtc, data);

        bool unhealthy = summary.CriticalFindingCount > 0 ||
            summary.ManifestVerificationFailureCount > 0 ||
            summary.OpenFindingCount >= _options.HealthUnhealthyFindingCount;
        if (unhealthy)
        {
            return HealthCheckResult.Unhealthy(
                "Critical audit-integrity findings require operator review.",
                data: data);
        }

        // Zero findings only means something when reconciliation is actually running. A worker that never completed a
        // run, stopped, or keeps failing would otherwise read as healthy indefinitely.
        if (freshnessProblem is not null)
        {
            return HealthCheckResult.Degraded(freshnessProblem, data: data);
        }

        bool degraded = summary.OpenFindingCount >= _options.HealthWarningFindingCount ||
            summary.StaleDeliveryCount > 0 ||
            summary.DeadLetterCount > 0 ||
            deliveryHealth?.DeadLetterCount > 0;
        return degraded
            ? HealthCheckResult.Degraded(
                "Audit reconciliation or delivery findings require attention.",
                data: data)
            : HealthCheckResult.Healthy("Audit integrity and delivery state are within configured thresholds.", data);
    }

    /// <summary>
    /// Returns the age after which a successful reconciliation run is considered stale.
    /// </summary>
    /// <param name="options">The reconciliation options.</param>
    /// <returns>The configured threshold, or three reconciliation intervals when none is configured.</returns>
    internal static TimeSpan GetStaleRunThreshold(ApplicationAuditReconciliationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.HealthStaleRunThreshold ?? (options.Interval * _defaultStaleRunIntervalMultiplier);
    }

    private string? EvaluateRunFreshness(DateTime? lastRunUtc, Dictionary<string, object> data)
    {
        if (!_options.RunWorker)
        {
            // The scheduled loop runs in another process, so this process cannot observe when reconciliation last ran.
            data["reconciliationFreshnessTracked"] = false;
            return null;
        }

        TimeSpan staleRunThreshold = GetStaleRunThreshold(_options);
        data["reconciliationFreshnessTracked"] = true;
        data["reconciliationStaleAfterSeconds"] = staleRunThreshold.TotalSeconds;

        if (!lastRunUtc.HasValue)
        {
            return NeverRunDescription;
        }

        TimeSpan sinceLastRun = _timeProvider.GetUtcNow().UtcDateTime - lastRunUtc.Value;
        data["secondsSinceLastReconciliation"] = Math.Max(sinceLastRun.TotalSeconds, 0);

        return sinceLastRun > staleRunThreshold ? StaleRunDescription : null;
    }
}
