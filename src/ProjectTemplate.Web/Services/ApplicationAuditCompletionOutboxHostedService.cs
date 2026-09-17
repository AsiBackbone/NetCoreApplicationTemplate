using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Web.Services;

/// <summary>
/// Dispatches durable audit-completion entries after their originating transaction commits.
/// </summary>
public sealed partial class ApplicationAuditCompletionOutboxHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ApplicationAuditCompletionOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<ApplicationAuditCompletionOutboxHostedService> logger)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ApplicationAuditCompletionOutboxOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<ApplicationAuditCompletionOutboxHostedService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    [LoggerMessage(
        EventId = 19100,
        Level = LogLevel.Error,
        Message = "The audit-completion outbox dispatch cycle failed and will be retried. ConsecutiveFailureCount: {ConsecutiveFailureCount}; RetryDelaySeconds: {RetryDelaySeconds}.")]
    private static partial void LogDispatchCycleFailure(
        ILogger logger,
        Exception exception,
        int consecutiveFailureCount,
        double retryDelaySeconds);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
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
                IApplicationAuditCompletionOutboxDispatcher dispatcher = scope.ServiceProvider
                    .GetRequiredService<IApplicationAuditCompletionOutboxDispatcher>();
                _ = await dispatcher.DispatchReadyAsync(stoppingToken).ConfigureAwait(false);
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
                _options.PollInterval,
                _options.MaximumCycleRetryDelay,
                consecutiveFailureCount);

            if (cycleFailure is not null)
            {
                LogDispatchCycleFailure(
                    _logger,
                    cycleFailure,
                    consecutiveFailureCount,
                    delay.TotalSeconds);
            }

            await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }
}
