using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Web.Services;

public sealed partial class ApplicationAuditReconciliationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ApplicationAuditReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<ApplicationAuditReconciliationHostedService> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ApplicationAuditReconciliationOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<ApplicationAuditReconciliationHostedService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    [LoggerMessage(
        EventId = 19110,
        Level = LogLevel.Error,
        Message = "The audit reconciliation cycle failed and will be retried. ConsecutiveFailureCount: {ConsecutiveFailureCount}; RetryDelaySeconds: {RetryDelaySeconds}.")]
    private static partial void LogReconciliationFailure(
        ILogger logger,
        Exception exception,
        int consecutiveFailureCount,
        double retryDelaySeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.RunWorker)
        {
            return;
        }

        int consecutiveFailureCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            Exception? cycleFailure = null;

            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IApplicationAuditReconciler reconciler = scope.ServiceProvider
                    .GetRequiredService<IApplicationAuditReconciler>();
                _ = await reconciler.ReconcileAsync(stoppingToken).ConfigureAwait(false);

                IApplicationAuditCompletionOutboxQuery? outboxQuery = scope.ServiceProvider
                    .GetService<IApplicationAuditCompletionOutboxQuery>();
                if (outboxQuery is not null)
                {
                    ApplicationAuditCompletionOutboxHealth deliveryHealth = await outboxQuery
                        .GetHealthAsync(stoppingToken)
                        .ConfigureAwait(false);
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationAuditReconciliationMetrics>()
                        .UpdateDelivery(deliveryHealth);
                }

                consecutiveFailureCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                cycleFailure = exception;
                consecutiveFailureCount++;
            }

            TimeSpan delay = BackgroundServiceRetryDelay.Calculate(
                _options.Interval,
                _options.MaximumCycleRetryDelay,
                consecutiveFailureCount);

            if (cycleFailure is not null)
            {
                LogReconciliationFailure(
                    _logger,
                    cycleFailure,
                    consecutiveFailureCount,
                    delay.TotalSeconds);
            }

            await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }
}
