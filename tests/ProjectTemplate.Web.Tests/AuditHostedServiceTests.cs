using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.Services;

namespace ProjectTemplate.Web.Tests;

public sealed class AuditHostedServiceTests
{
    [Fact]
    public async Task CompletionOutboxWorker_Disabled_DoesNotDispatch()
    {
        var dispatcher = new BlockingDispatcher();
        using ServiceProvider provider = CreateProvider(services => services.AddSingleton<IApplicationAuditCompletionOutboxDispatcher>(dispatcher));
        var service = new ApplicationAuditCompletionOutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions { Enabled = false }),
            TimeProvider.System,
            NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, dispatcher.CallCount);
    }

    [Fact]
    public async Task CompletionOutboxWorker_DispatchesUntilStopped()
    {
        var dispatcher = new BlockingDispatcher();
        using ServiceProvider provider = CreateProvider(services => services.AddSingleton<IApplicationAuditCompletionOutboxDispatcher>(dispatcher));
        var service = new ApplicationAuditCompletionOutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(1),
                MaximumCycleRetryDelay = TimeSpan.FromMilliseconds(2)
            }),
            TimeProvider.System,
            NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await dispatcher.Called.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, dispatcher.CallCount);
    }

    [Fact]
    public async Task CompletionOutboxWorker_LogsFailureAndRetries()
    {
        var dispatcher = new FailThenBlockDispatcher();
        using ServiceProvider provider = CreateProvider(services => services.AddSingleton<IApplicationAuditCompletionOutboxDispatcher>(dispatcher));
        var logger = new RecordingLogger<ApplicationAuditCompletionOutboxHostedService>();
        var service = new ApplicationAuditCompletionOutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(1),
                MaximumCycleRetryDelay = TimeSpan.FromMilliseconds(2)
            }),
            TimeProvider.System,
            logger);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await dispatcher.SecondCall.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task ReconciliationWorker_DisabledOrWorkerOff_DoesNotReconcile()
    {
        var reconciler = new BlockingReconciler();
        using ServiceProvider provider = CreateProvider(services => services.AddSingleton<IApplicationAuditReconciler>(reconciler));

        foreach (ApplicationAuditReconciliationOptions options in new[]
                 {
                     new ApplicationAuditReconciliationOptions { Enabled = false },
                     new ApplicationAuditReconciliationOptions { Enabled = true, RunWorker = false }
                 })
        {
            var service = new ApplicationAuditReconciliationHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(options),
                TimeProvider.System,
                NullLogger<ApplicationAuditReconciliationHostedService>.Instance);
            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, reconciler.CallCount);
    }

    [Fact]
    public async Task ReconciliationWorker_UpdatesDeliveryMetricsUntilStopped()
    {
        var reconciler = new CompletingReconciler();
        var query = new BlockingOutboxQuery();
        var pendingMeasurements = new List<long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Name == "ncat.audit.outbox.pending")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => pendingMeasurements.Add(value));
        listener.Start();
        using var metrics = new ApplicationAuditReconciliationMetrics();
        using ServiceProvider provider = CreateProvider(services =>
        {
            services.AddSingleton<IApplicationAuditReconciler>(reconciler);
            services.AddSingleton<IApplicationAuditCompletionOutboxQuery>(query);
            services.AddSingleton(metrics);
        });
        var service = new ApplicationAuditReconciliationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions
            {
                Interval = TimeSpan.FromMilliseconds(1),
                MaximumCycleRetryDelay = TimeSpan.FromMilliseconds(2)
            }),
            TimeProvider.System,
            NullLogger<ApplicationAuditReconciliationHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await query.SecondCall.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        listener.RecordObservableInstruments();

        Assert.Equal(2, reconciler.CallCount);
        Assert.Contains(1, pendingMeasurements);
    }

    [Fact]
    public async Task ReconciliationWorker_LogsFailureAndRetries()
    {
        var reconciler = new FailThenBlockReconciler();
        using var metrics = new ApplicationAuditReconciliationMetrics();
        using ServiceProvider provider = CreateProvider(services =>
        {
            services.AddSingleton<IApplicationAuditReconciler>(reconciler);
            services.AddSingleton(metrics);
        });
        var logger = new RecordingLogger<ApplicationAuditReconciliationHostedService>();
        var service = new ApplicationAuditReconciliationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions
            {
                Interval = TimeSpan.FromMilliseconds(1),
                MaximumCycleRetryDelay = TimeSpan.FromMilliseconds(2)
            }),
            TimeProvider.System,
            logger);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await reconciler.SecondCall.Task.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public void HostedServices_RejectNullDependencies()
    {
        using ServiceProvider provider = CreateProvider(_ => { });

        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditCompletionOutboxHostedService(
            null!, Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions()), TimeProvider.System,
            NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationAuditReconciliationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(), null!, TimeProvider.System,
            NullLogger<ApplicationAuditReconciliationHostedService>.Instance));
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        ServiceCollection services = new();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class BlockingDispatcher : IApplicationAuditCompletionOutboxDispatcher
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<ApplicationAuditCompletionDispatchSummary> DispatchReadyAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            Called.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(true, 0, 0, 0, 0, 0, 0);
        }
    }

    private sealed class FailThenBlockDispatcher : IApplicationAuditCompletionOutboxDispatcher
    {
        private int _callCount;

        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ApplicationAuditCompletionDispatchSummary> DispatchReadyAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                throw new InvalidOperationException("Expected dispatch failure.");
            }

            SecondCall.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(true, 0, 0, 0, 0, 0, 0);
        }
    }

    private sealed class BlockingReconciler : IApplicationAuditReconciler
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<ApplicationAuditReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            Called.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ApplicationAuditReconciliationMetrics.DisabledSummary;
        }

        public Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ApplicationAuditReconciliationMetrics.DisabledSummary);
        }

        public Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
            ApplicationAuditReconciliationQuery request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApplicationAuditReconciliationFindingItem>>([]);
        }

        public Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
            Guid findingId,
            ApplicationAuditReconciliationRemediationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class CompletingReconciler : IApplicationAuditReconciler
    {
        public int CallCount { get; private set; }

        public Task<ApplicationAuditReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ApplicationAuditReconciliationSummary(true, DateTime.UtcNow, 0, 0, 0, 0, 0, 0, 0));
        }

        public Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ApplicationAuditReconciliationMetrics.DisabledSummary);
        }

        public Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
            ApplicationAuditReconciliationQuery request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApplicationAuditReconciliationFindingItem>>([]);
        }

        public Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
            Guid findingId,
            ApplicationAuditReconciliationRemediationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailThenBlockReconciler : IApplicationAuditReconciler
    {
        private int _callCount;

        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ApplicationAuditReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                throw new InvalidOperationException("Expected reconciliation failure.");
            }

            SecondCall.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ApplicationAuditReconciliationMetrics.DisabledSummary;
        }

        public Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ApplicationAuditReconciliationMetrics.DisabledSummary);
        }

        public Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
            ApplicationAuditReconciliationQuery request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApplicationAuditReconciliationFindingItem>>([]);
        }

        public Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
            Guid findingId,
            ApplicationAuditReconciliationRemediationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingOutboxQuery : IApplicationAuditCompletionOutboxQuery
    {
        private int _callCount;

        public TaskCompletionSource SecondCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ApplicationAuditCompletionOutboxHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                return new(true, 1, TimeSpan.FromSeconds(1), 0, 0);
            }

            SecondCall.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new(true, 1, TimeSpan.FromSeconds(1), 0, 0);
        }

        public Task<IReadOnlyList<ApplicationAuditCompletionOutboxItem>> QueryAsync(
            ApplicationAuditCompletionOutboxQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApplicationAuditCompletionOutboxItem>>([]);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception));
        }
    }
}
