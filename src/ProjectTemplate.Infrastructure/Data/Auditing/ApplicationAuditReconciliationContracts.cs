namespace ProjectTemplate.Infrastructure.Data.Auditing;

public static class ApplicationAuditReconciliationReasonCodes
{
    public const string MissingCompletion = "MissingCompletion";
    public const string MissingAuditBatch = "MissingAuditBatch";
    public const string AuditRecordCountMismatch = "AuditRecordCountMismatch";
    public const string ManifestVerificationFailed = "ManifestVerificationFailed";
    public const string IncompleteGeneratedValues = "IncompleteGeneratedValues";
    public const string MalformedCorrelation = "MalformedCorrelation";
    public const string DuplicateCompletion = "DuplicateCompletion";
    public const string StalePending = "StalePending";
    public const string StaleRetryReady = "StaleRetryReady";
    public const string DeliveryFailed = "DeliveryFailed";
    public const string DeadLettered = "DeadLettered";
}

public static class ApplicationAuditReconciliationSeverities
{
    public const string Information = "Information";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Critical = "Critical";
}

public static class ApplicationAuditReconciliationRemediationStatuses
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";
}

public sealed class ApplicationAuditReconciliationOptions
{
    public bool Enabled { get; set; } = true;

    public bool RunWorker { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum delay between reconciliation cycles after consecutive failures. The delay doubles for
    /// each additional consecutive failure, up to this value, and returns to <see cref="Interval"/> after a success.
    /// </summary>
    public TimeSpan MaximumCycleRetryDelay { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan CompletionGracePeriod { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan StalePendingThreshold { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan StaleRetryReadyThreshold { get; set; } = TimeSpan.FromMinutes(15);

    public int MaximumBatchesPerRun { get; set; } = 1_000;

    public int MaximumMalformedRecordsPerRun { get; set; } = 1_000;

    public int HealthWarningFindingCount { get; set; } = 1;

    public int HealthUnhealthyFindingCount { get; set; } = 10;
}

public interface IApplicationAuditReconciler
{
    Task<ApplicationAuditReconciliationSummary> ReconcileAsync(
        CancellationToken cancellationToken = default);

    Task<ApplicationAuditReconciliationSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationAuditReconciliationFindingItem>> QueryFindingsAsync(
        ApplicationAuditReconciliationQuery request,
        CancellationToken cancellationToken = default);

    Task<ApplicationAuditReconciliationRemediationItem> RecordRemediationAsync(
        Guid findingId,
        ApplicationAuditReconciliationRemediationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ApplicationAuditReconciliationSummary(
    bool Enabled,
    DateTime? LastRunUtc,
    long OpenFindingCount,
    long ErrorFindingCount,
    long CriticalFindingCount,
    long ManifestVerificationFailureCount,
    long MissingCompletionCount,
    long StaleDeliveryCount,
    long DeadLetterCount);

public sealed record ApplicationAuditReconciliationQuery(
    string? ReasonCode = null,
    string? Severity = null,
    string? MutationBatchId = null,
    string? RemediationStatus = null,
    int MaximumResults = 100);

public sealed record ApplicationAuditReconciliationFindingItem(
    Guid Id,
    string SchemaVersion,
    string FindingKey,
    string ReasonCode,
    string Severity,
    string MutationBatchId,
    string? Destination,
    string Guidance,
    string RemediationStatus,
    DateTime FirstObservedUtc,
    DateTime LastObservedUtc,
    DateTime? ResolvedUtc);

public sealed record ApplicationAuditReconciliationRemediationRequest(
    string ActionCode,
    string ActorId,
    string? EvidenceReference = null,
    bool ResolveFinding = false);

public sealed record ApplicationAuditReconciliationRemediationItem(
    Guid Id,
    Guid FindingId,
    string MutationBatchId,
    string ActionCode,
    string ActorId,
    string? EvidenceReference,
    DateTime RecordedUtc);

internal sealed record ApplicationAuditReconciliationCandidate(
    string FindingKey,
    string ReasonCode,
    string Severity,
    string MutationBatchId,
    string? Destination,
    string Guidance);
