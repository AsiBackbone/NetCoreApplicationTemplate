using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Extensions;
using ProjectTemplate.Web.Extensions;
using ProjectTemplate.Web.HealthChecks;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides tests for the audit integrity health check, including reconciliation freshness and endpoint placement.
/// </summary>
public sealed class ApplicationAuditIntegrityHealthCheckTests
{
    private static readonly DateTime _now = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Verifies that a process whose worker never completed a run reports Degraded instead of Healthy.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_NoCompletedRun_ReportsDegraded()
    {
        HealthCheckResult result = await CheckAsync(CreateSummary(lastRunUtc: null), CreateOptions());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(ApplicationAuditIntegrityHealthCheck.NeverRunDescription, result.Description);
        Assert.True((bool)result.Data["reconciliationFreshnessTracked"]);
    }

    /// <summary>
    /// Verifies that a recent successful run with no findings reports Healthy.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_RecentRun_ReportsHealthy()
    {
        HealthCheckResult result = await CheckAsync(
            CreateSummary(lastRunUtc: _now.AddMinutes(-1)),
            CreateOptions());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(60d, (double)result.Data["secondsSinceLastReconciliation"]);
    }

    /// <summary>
    /// Verifies that a run older than the default threshold of three intervals reports Degraded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_RunOlderThanDefaultThreshold_ReportsDegraded()
    {
        HealthCheckResult result = await CheckAsync(
            CreateSummary(lastRunUtc: _now.AddMinutes(-16)),
            CreateOptions());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(ApplicationAuditIntegrityHealthCheck.StaleRunDescription, result.Description);
        Assert.Equal(900d, (double)result.Data["reconciliationStaleAfterSeconds"]);
    }

    /// <summary>
    /// Verifies that a configured stale-run threshold replaces the default.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_ConfiguredThreshold_IsHonored()
    {
        ApplicationAuditReconciliationOptions options = CreateOptions();
        options.HealthStaleRunThreshold = TimeSpan.FromHours(1);

        HealthCheckResult result = await CheckAsync(
            CreateSummary(lastRunUtc: _now.AddMinutes(-30)),
            options);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// Verifies that freshness is not evaluated when another process runs the reconciliation loop.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_WorkerNotRunInProcess_DoesNotEvaluateFreshness()
    {
        ApplicationAuditReconciliationOptions options = CreateOptions();
        options.RunWorker = false;

        HealthCheckResult result = await CheckAsync(CreateSummary(lastRunUtc: null), options);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.False((bool)result.Data["reconciliationFreshnessTracked"]);
    }

    /// <summary>
    /// Verifies that a critical finding still reports Unhealthy when reconciliation is also stale.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_CriticalFindingAndStaleRun_ReportsUnhealthy()
    {
        HealthCheckResult result = await CheckAsync(
            CreateSummary(lastRunUtc: _now.AddHours(-2), criticalFindings: 1),
            CreateOptions());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    /// <summary>
    /// Verifies that disabled reconciliation reports Healthy without evaluating freshness.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task CheckHealthAsync_ReconciliationDisabled_ReportsHealthy()
    {
        ApplicationAuditReconciliationOptions options = CreateOptions();
        options.Enabled = false;

        HealthCheckResult result = await CheckAsync(CreateSummary(lastRunUtc: null), options);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// Verifies that the default stale-run threshold is three reconciliation intervals.
    /// </summary>
    [Fact]
    public void GetStaleRunThreshold_NotConfigured_UsesThreeIntervals()
    {
        ApplicationAuditReconciliationOptions options = CreateOptions();
        options.Interval = TimeSpan.FromMinutes(7);

        TimeSpan threshold = ApplicationAuditIntegrityHealthCheck.GetStaleRunThreshold(options);

        Assert.Equal(TimeSpan.FromMinutes(21), threshold);
    }

    /// <summary>
    /// Verifies that the audit integrity check is excluded from readiness and selected by the audit endpoint tag.
    /// </summary>
    [Fact]
    public void AddApplicationAuditReconciliation_RegistersCheckOutsideReadiness()
    {
        ServiceCollection services = new();
        _ = services.AddApplicationAuditReconciliation();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        HealthCheckRegistration registration = Assert.Single(
            provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
                .Value
                .Registrations,
            candidate => candidate.Name == "application-audit-integrity");

        Assert.Contains(ApplicationHealthCheckTags.Audit, registration.Tags);
        Assert.Contains(ApplicationHealthCheckTags.Integrity, registration.Tags);
        Assert.DoesNotContain(ApplicationHealthCheckTags.Ready, registration.Tags);
    }

    /// <summary>
    /// Verifies that startup validation rejects a stale-run threshold that is not greater than the interval.
    /// </summary>
    [Fact]
    public void AddApplicationAuditReconciliationCore_ThresholdNotGreaterThanInterval_FailsValidation()
    {
        ServiceCollection services = new();
        _ = services.AddApplicationAuditReconciliationCore(options =>
        {
            options.Interval = TimeSpan.FromMinutes(5);
            options.HealthStaleRunThreshold = TimeSpan.FromMinutes(5);
        });

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Microsoft.Extensions.Options.OptionsValidationException exception =
            Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
                provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ApplicationAuditReconciliationOptions>>()
                    .Value);

        Assert.Contains(
            "The health stale-run threshold must be greater than the reconciliation interval.",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static async Task<HealthCheckResult> CheckAsync(
        ApplicationAuditReconciliationSummary summary,
        ApplicationAuditReconciliationOptions options)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IApplicationAuditReconciler>(new StubReconciler(summary));

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        ApplicationAuditIntegrityHealthCheck healthCheck = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options),
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

    private static ApplicationAuditReconciliationSummary CreateSummary(
        DateTime? lastRunUtc,
        long criticalFindings = 0)
    {
        return new ApplicationAuditReconciliationSummary(
            Enabled: true,
            LastRunUtc: lastRunUtc,
            OpenFindingCount: criticalFindings,
            ErrorFindingCount: 0,
            CriticalFindingCount: criticalFindings,
            ManifestVerificationFailureCount: 0,
            MissingCompletionCount: 0,
            StaleDeliveryCount: 0,
            DeadLetterCount: 0);
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
