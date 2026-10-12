using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.HealthChecks;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the constructor guards and the delivery-health branches of <c>ApplicationAuditIntegrityHealthCheck</c>.
/// </summary>
public sealed class ApplicationAuditIntegrityHealthCheckBranchCoverageTests
{
    private static readonly DateTime _now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        ArgumentNullException scopeFactoryException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditIntegrityHealthCheck(
                null!,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                TimeProvider.System));
        ArgumentNullException optionsException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditIntegrityHealthCheck(scopeFactory, null!, TimeProvider.System));
        ArgumentNullException timeProviderException = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationAuditIntegrityHealthCheck(
                scopeFactory,
                Microsoft.Extensions.Options.Options.Create(CreateOptions()),
                null!));

        Assert.Equal("scopeFactory", scopeFactoryException.ParamName);
        Assert.Equal("options", optionsException.ParamName);
        Assert.Equal("timeProvider", timeProviderException.ParamName);
    }

    [Fact]
    public async Task CheckHealthAsync_DeliveryHealthWithDeadLetters_ReportsDegradedWithDeliveryData()
    {
        HealthCheckResult result = await CheckAsync(
            new ApplicationAuditCompletionOutboxHealth(
                Enabled: true,
                BacklogCount: 3,
                OldestPendingAge: TimeSpan.FromSeconds(90),
                TotalRetryCount: 4,
                DeadLetterCount: 1));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(3L, (long)Assert.Contains("outboxBacklog", result.Data));
        Assert.Equal(4L, (long)Assert.Contains("outboxRetryCount", result.Data));
        Assert.Equal(1L, (long)Assert.Contains("outboxDeadLetters", result.Data));
        Assert.Equal(90d, (double)Assert.Contains("oldestPendingAgeSeconds", result.Data));
    }

    [Fact]
    public async Task CheckHealthAsync_DeliveryHealthWithoutPendingAge_ReportsHealthyWithoutAgeData()
    {
        HealthCheckResult result = await CheckAsync(
            new ApplicationAuditCompletionOutboxHealth(
                Enabled: true,
                BacklogCount: 0,
                OldestPendingAge: null,
                TotalRetryCount: 0,
                DeadLetterCount: 0));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0L, (long)Assert.Contains("outboxBacklog", result.Data));
        Assert.DoesNotContain("oldestPendingAgeSeconds", result.Data);
    }

    private static async Task<HealthCheckResult> CheckAsync(ApplicationAuditCompletionOutboxHealth deliveryHealth)
    {
        using var metrics = new ApplicationAuditReconciliationMetrics();
        ServiceCollection services = new();
        _ = services.AddSingleton<IApplicationAuditReconciler>(new StubReconciler(new ApplicationAuditReconciliationSummary(
            Enabled: true,
            LastRunUtc: _now.AddMinutes(-1),
            OpenFindingCount: 0,
            ErrorFindingCount: 0,
            CriticalFindingCount: 0,
            ManifestVerificationFailureCount: 0,
            MissingCompletionCount: 0,
            StaleDeliveryCount: 0,
            DeadLetterCount: 0)));
        _ = services.AddSingleton<IApplicationAuditCompletionOutboxQuery>(new StubOutboxQuery(deliveryHealth));
        _ = services.AddSingleton(metrics);

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        ApplicationAuditIntegrityHealthCheck healthCheck = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(CreateOptions()),
            new FixedTimeProvider(new DateTimeOffset(_now)));

        return await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);
    }

    private static ApplicationAuditReconciliationOptions CreateOptions()
    {
        return new ApplicationAuditReconciliationOptions
        {
            Enabled = true,
            RunWorker = true,
            Interval = TimeSpan.FromMinutes(5)
        };
    }

    private sealed class StubReconciler(ApplicationAuditReconciliationSummary summary) : IApplicationAuditReconciler
    {
        public Task<ApplicationAuditReconciliationSummary> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(summary);
        }

        public Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(summary);
        }

        public Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
            ApplicationAuditReconciliationQuery request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
            Guid findingId,
            ApplicationAuditReconciliationRemediationRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubOutboxQuery(ApplicationAuditCompletionOutboxHealth health)
        : IApplicationAuditCompletionOutboxQuery
    {
        public Task<ApplicationAuditCompletionOutboxHealth> GetHealthAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(health);
        }

        public Task<IReadOnlyList<ApplicationAuditCompletionOutboxItem>> QueryAsync(
            ApplicationAuditCompletionOutboxQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
