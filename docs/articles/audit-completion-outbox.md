# Durable Audit-Completion Outbox

NCAT provides an optional, provider-neutral local outbox for minimized `ApplicationMutationAuditReceipt` handoffs. It closes the process-failure gap between committing a mutation and allowing an external accountability, archive, telemetry, or governance adapter to accept the receipt.

The outbox is a durable local handoff. It is not a distributed queue, does not make an external destination part of the business transaction, and does not promise exactly-once delivery.

## Registration

The feature is opt-in. Web applications can register the audited transaction coordinator, durable outbox services, and hosted dispatcher together:

```csharp
services.AddApplicationDataAccess(configuration);
services.AddApplicationAuditCompletionOutbox(options =>
{
    options.DefaultDestination = "accountability-archive";
    options.BatchSize = 25;
    options.MaxRetryAttempts = 5;
});
```

Infrastructure-only consumers can register staging, dispatcher, and query services without starting a hosted loop:

```csharp
services.AddApplicationInfrastructureDataAccess(configuration);
services.AddApplicationAuditedTransactions();
services.AddApplicationAuditCompletionOutboxCore();
```

Set `Enabled = false` to leave registered consumers dormant. Applications that never call either registration method retain the existing lightweight NCAT behavior.

## Atomic staging

Stage the outbox entry through the local-completion callback supplied by `IApplicationAuditedTransaction`. The entry then participates in the same application database transaction as the business mutation, NCAT audit rows, generated-value audit completion, and receipt creation.

```csharp
ApplicationAuditedTransactionResult result = await auditedTransaction.ExecuteAsync(
    (dbContext, cancellationToken) =>
    {
        dbContext.Add(order);
        return Task.CompletedTask;
    },
    async (dbContext, receipt, cancellationToken) =>
    {
        await completionOutbox.StageAsync(
            dbContext,
            receipt,
            destination: "accountability-archive",
            cancellationToken);
    },
    cancellationToken: cancellationToken);
```

`Stage` and `StageAsync` only add the minimized handoff to the supplied `ApplicationDbContext`. They do not call an external system. The transaction coordinator performs the save and owns or joins the transaction according to its documented rules.

## Publisher adapters

A destination adapter implements `IApplicationAuditCompletionPublisher`:

```csharp
public sealed class ArchiveCompletionPublisher : IApplicationAuditCompletionPublisher
{
    public string Destination => "accountability-archive";

    public async ValueTask<ApplicationAuditCompletionPublishResult> PublishAsync(
        ApplicationAuditCompletionMessage message,
        CancellationToken cancellationToken = default)
    {
        await SendToArchiveAsync(message, cancellationToken);
        return ApplicationAuditCompletionPublishResult.Success();
    }
}
```

Register adapters with their normal DI lifetime. Each destination may have only one publisher registration. Adapters own downstream authentication, protocol mapping, error classification, and use of the supplied idempotency key.

Duplicate delivery attempts are possible after process failure or an ambiguous downstream response. Publishers should make repeated calls with the same `IdempotencyKey` safe.

## Delivery states and retry policy

Entries begin in `Pending`. The dispatcher transitions them to:

- `Delivered` after an accepted handoff;
- `RetryableFailure` when exponential retry is scheduled;
- `Failed` for a terminal non-retryable failure;
- `Deferred` when no publisher is installed or an adapter intentionally postpones delivery;
- `DeadLettered` after retryable failures reach `MaxRetryAttempts`.

Retry delay begins at `BaseRetryDelay`, doubles for each retry, and is capped by `MaxRetryDelay`. A publisher can provide a specific `RetryAfter` value. Deferred entries use `DeferredRetryDelay` by default.

The dispatcher performs publication outside the business transaction. It persists each resulting state transition separately so completed work survives later failures or host shutdown.

## Minimized data boundary

The durable record and publisher message contain only:

- receipt and outbox schema versions;
- destination and stable idempotency key;
- mutation batch ID and audit record count;
- persistence outcome and completion timestamp;
- manifest hash, algorithm, and schema version;
- opaque operation, attempt, decision, correlation, and trace identifiers;
- delivery state, timestamps, retry count, and bounded error diagnostics.

They do not contain audited entity keys, original values, or current values. Destination adapters should preserve this boundary and must not rehydrate raw audit values merely to deliver a completion notification.

Adapter authors can test their message handling against the published [audit-completion contract vectors](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/contracts/audit-completion/README.md). These vectors pin the message fields, idempotency key derivation, and canonical manifest digests NCAT produces.

## No-publisher operation

NCAT continues to operate when no external publisher or governance product is installed. Ready entries are moved to `Deferred` and scheduled for a later check rather than causing the web host or business mutation to fail.

## Health and operator queries

`IApplicationAuditCompletionOutboxQuery.GetHealthAsync` returns:

- current backlog count;
- age of the oldest pending, retryable, or deferred entry;
- cumulative retry count;
- dead-letter count.

`QueryAsync` returns bounded, minimized operator projections filtered by status, destination, or mutation batch ID. These contracts can back an authenticated administrative endpoint or a custom health check without exposing mutation values.

## Operational guidance

Run the EF Core migration before enabling staging. Monitor backlog age and dead-letter count, secure any administrative query surface, and define a documented process for investigating and replaying terminal failures. Multi-instance hosts should treat duplicate attempts as normal and rely on the database uniqueness constraints plus destination idempotency; the local outbox is not a lease-based distributed queue.
