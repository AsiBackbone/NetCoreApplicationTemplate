using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.Services;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the disabled early-return and constructor guard branches of the audit background services.
/// </summary>
/// <remarks>
/// <c>BackgroundService.StartAsync</c> schedules <c>ExecuteAsync</c> on the thread pool with the stopping token, so a
/// test that stops the service immediately can cancel execution before the disabled check runs. These tests wait for
/// <c>ExecuteTask</c> to finish before stopping, which guarantees that the disabled branch executes.
/// </remarks>
public sealed class AuditHostedServiceBranchCoverageTests
{
    [Fact]
    public async Task CompletionOutboxWorker_Disabled_CompletesExecutionWithoutDispatching()
    {
        var dispatcher = new CountingDispatcher();
        await using ServiceProvider provider = CreateProvider(services =>
            services.AddSingleton<IApplicationAuditCompletionOutboxDispatcher>(dispatcher));
        using var service = new ApplicationAuditCompletionOutboxHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions { Enabled = false }),
            TimeProvider.System,
            NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(service.ExecuteTask);
        await service.ExecuteTask.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ReconciliationWorker_DisabledOrWorkerOff_CompletesExecutionWithoutReconciling(
        bool enabled,
        bool runWorker)
    {
        var reconciler = new CountingReconciler();
        await using ServiceProvider provider = CreateProvider(services =>
            services.AddSingleton<IApplicationAuditReconciler>(reconciler));
        using var service = new ApplicationAuditReconciliationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions
            {
                Enabled = enabled,
                RunWorker = runWorker
            }),
            TimeProvider.System,
            NullLogger<ApplicationAuditReconciliationHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(service.ExecuteTask);
        await service.ExecuteTask.WaitAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(0, reconciler.CallCount);
    }

    [Fact]
    public void CompletionOutboxWorker_NullDependency_ThrowsArgumentNullException()
    {
        using ServiceProvider provider = CreateProvider(_ => { });
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        ArgumentNullException optionsException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditCompletionOutboxHostedService(
                scopeFactory,
                null!,
                TimeProvider.System,
                NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance));
        ArgumentNullException timeProviderException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditCompletionOutboxHostedService(
                scopeFactory,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions()),
                null!,
                NullLogger<ApplicationAuditCompletionOutboxHostedService>.Instance));
        ArgumentNullException loggerException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditCompletionOutboxHostedService(
                scopeFactory,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditCompletionOutboxOptions()),
                TimeProvider.System,
                null!));

        Assert.Equal("options", optionsException.ParamName);
        Assert.Equal("timeProvider", timeProviderException.ParamName);
        Assert.Equal("logger", loggerException.ParamName);
    }

    [Fact]
    public void ReconciliationWorker_NullDependency_ThrowsArgumentNullException()
    {
        using ServiceProvider provider = CreateProvider(_ => { });
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        ArgumentNullException scopeFactoryException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditReconciliationHostedService(
                null!,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
                TimeProvider.System,
                NullLogger<ApplicationAuditReconciliationHostedService>.Instance));
        ArgumentNullException timeProviderException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditReconciliationHostedService(
                scopeFactory,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
                null!,
                NullLogger<ApplicationAuditReconciliationHostedService>.Instance));
        ArgumentNullException loggerException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditReconciliationHostedService(
                scopeFactory,
                Microsoft.Extensions.Options.Options.Create(new ApplicationAuditReconciliationOptions()),
                TimeProvider.System,
                null!));

        Assert.Equal("scopeFactory", scopeFactoryException.ParamName);
        Assert.Equal("timeProvider", timeProviderException.ParamName);
        Assert.Equal("logger", loggerException.ParamName);
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        ServiceCollection services = new();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class CountingDispatcher : IApplicationAuditCompletionOutboxDispatcher
    {
        public int CallCount { get; private set; }

        public Task<ApplicationAuditCompletionDispatchSummary> DispatchReadyAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ApplicationAuditCompletionDispatchSummary(true, 0, 0, 0, 0, 0, 0));
        }
    }

    private sealed class CountingReconciler : IApplicationAuditReconciler
    {
        public int CallCount { get; private set; }

        public Task<ApplicationAuditReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ApplicationAuditReconciliationMetrics.DisabledSummary);
        }

        public Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(
            CancellationToken cancellationToken = default)
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
}
