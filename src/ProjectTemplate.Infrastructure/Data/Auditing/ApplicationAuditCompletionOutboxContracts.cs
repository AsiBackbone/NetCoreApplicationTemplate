using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Identifies durable audit-completion delivery states.
/// </summary>
public static class ApplicationAuditCompletionOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string RetryableFailure = "RetryableFailure";
    public const string Failed = "Failed";
    public const string Deferred = "Deferred";
    public const string DeadLettered = "DeadLettered";
}

/// <summary>
/// Configures the optional durable audit-completion outbox and dispatcher.
/// </summary>
public sealed class ApplicationAuditCompletionOutboxOptions
{
    public bool Enabled { get; set; } = true;

    public string DefaultDestination { get; set; } = "default";

    public int BatchSize { get; set; } = 25;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum delay between dispatch cycles after consecutive failures. The delay doubles for each
    /// additional consecutive failure, up to this value, and returns to <see cref="PollInterval"/> after a success.
    /// </summary>
    public TimeSpan MaximumCycleRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan DeferredRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    public int MaxRetryAttempts { get; set; } = 5;

    public int MaxErrorDetailLength { get; set; } = 512;
}

/// <summary>
/// Stages minimized audit-completion receipts in the application database.
/// </summary>
public interface IApplicationAuditCompletionOutbox
{
    ApplicationAuditCompletionOutboxEntry? Stage(
        ApplicationDbContext dbContext,
        ApplicationMutationAuditReceipt receipt,
        string? destination = null);

    ValueTask<ApplicationAuditCompletionOutboxEntry?> StageAsync(
        ApplicationDbContext dbContext,
        ApplicationMutationAuditReceipt receipt,
        string? destination = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Publishes one minimized audit-completion message to a host-owned destination.
/// </summary>
public interface IApplicationAuditCompletionPublisher
{
    string Destination { get; }

    ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
        ApplicationAuditCompletionMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches ready durable audit-completion entries after their business transaction commits.
/// </summary>
public interface IApplicationAuditCompletionOutboxDispatcher
{
    Task<ApplicationAuditCompletionDispatchSummary> DispatchReadyAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exposes minimized operational status and query projections for the durable outbox.
/// </summary>
public interface IApplicationAuditCompletionOutboxQuery
{
    Task<ApplicationAuditCompletionOutboxHealth> GetHealthAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationAuditCompletionOutboxItem>> QueryAsync(
        ApplicationAuditCompletionOutboxQueryRequest request,
        CancellationToken cancellationToken = default);
}

public enum ApplicationAuditCompletionPublishDisposition
{
    Delivered,
    RetryableFailure,
    Failed,
    Deferred
}

public sealed record ApplicationAuditCompletionPublishResult(
    ApplicationAuditCompletionPublishDisposition Disposition,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    TimeSpan? RetryAfter = null)
{
    public static ApplicationAuditCompletionPublishResult Success()
    {
        return new(ApplicationAuditCompletionPublishDisposition.Delivered);
    }

    public static ApplicationAuditCompletionPublishResult Retry(
        string? errorCode = null,
        string? errorMessage = null,
        TimeSpan? retryAfter = null)
    {
        return new(
            ApplicationAuditCompletionPublishDisposition.RetryableFailure,
            errorCode,
            errorMessage,
            retryAfter);
    }

    public static ApplicationAuditCompletionPublishResult TerminalFailure(
        string? errorCode = null,
        string? errorMessage = null)
    {
        return new(ApplicationAuditCompletionPublishDisposition.Failed, errorCode, errorMessage);
    }

    public static ApplicationAuditCompletionPublishResult Defer(
        string? errorCode = null,
        string? errorMessage = null,
        TimeSpan? retryAfter = null)
    {
        return new(
            ApplicationAuditCompletionPublishDisposition.Deferred,
            errorCode,
            errorMessage,
            retryAfter);
    }
}

public sealed record ApplicationAuditCompletionMessage(
    string SchemaVersion,
    string Destination,
    string IdempotencyKey,
    string MutationBatchId,
    int AuditRecordCount,
    string PersistenceOutcome,
    DateTime ReceiptCompletedUtc,
    string MutationManifestHash,
    string MutationManifestAlgorithm,
    string MutationManifestSchemaVersion,
    string? OperationExecutionId,
    string? ExecutionAttemptId,
    string? DecisionAuditRecordId,
    string? CorrelationId,
    string? TraceId);

public sealed record ApplicationAuditCompletionDispatchSummary(
    bool Enabled,
    int AttemptedCount,
    int DeliveredCount,
    int RetryScheduledCount,
    int FailedCount,
    int DeferredCount,
    int DeadLetteredCount);

public sealed record ApplicationAuditCompletionOutboxHealth(
    bool Enabled,
    long BacklogCount,
    TimeSpan? OldestPendingAge,
    long TotalRetryCount,
    long DeadLetterCount);

public sealed record ApplicationAuditCompletionOutboxQueryRequest(
    string? Status = null,
    string? Destination = null,
    string? MutationBatchId = null,
    int MaximumResults = 100);

public sealed record ApplicationAuditCompletionOutboxItem(
    Guid Id,
    string SchemaVersion,
    string Destination,
    string IdempotencyKey,
    string MutationBatchId,
    int AuditRecordCount,
    string PersistenceOutcome,
    DateTime ReceiptCompletedUtc,
    string MutationManifestHash,
    string MutationManifestAlgorithm,
    string MutationManifestSchemaVersion,
    string? OperationExecutionId,
    string? ExecutionAttemptId,
    string? DecisionAuditRecordId,
    string? CorrelationId,
    string? TraceId,
    string Status,
    int RetryCount,
    DateTime CreatedUtc,
    DateTime? LastAttemptUtc,
    DateTime? NextAttemptUtc,
    DateTime? DeliveredUtc,
    string? LastErrorCode,
    string? LastErrorMessage);
