# Audit Reconciliation, Integrity Health, and Recovery

NCAT can optionally reconcile retained `AuditRecord` batches with durable audit-completion outbox records. The feature is application-owned and provider-neutral. It does not require AsiBackbone or any external governance product.

Reconciliation is an operational control, not a replacement for transactional persistence. It detects evidence relationships that are missing, inconsistent, altered, or stalled after the original transaction has completed.

## Registration

Register the durable audit-completion outbox before reconciliation when the application uses local completion delivery:

```csharp
builder.Services.AddApplicationAuditCompletionOutbox();
builder.Services.AddApplicationAuditReconciliation(options =>
{
    options.Enabled = true;
    options.RunWorker = true;
    options.Interval = TimeSpan.FromMinutes(5);
    options.MaximumCycleRetryDelay = TimeSpan.FromMinutes(30);
    options.CompletionGracePeriod = TimeSpan.FromMinutes(2);
    options.StalePendingThreshold = TimeSpan.FromMinutes(15);
    options.StaleRetryReadyThreshold = TimeSpan.FromMinutes(15);
    options.HealthWarningFindingCount = 1;
    options.HealthUnhealthyFindingCount = 10;
});
```

When a reconciliation cycle throws, the worker waits longer before the next attempt: the delay doubles for each additional consecutive failure, up to `MaximumCycleRetryDelay`, and returns to `Interval` after a cycle succeeds. The failure log entry reports `ConsecutiveFailureCount` and `RetryDelaySeconds`. The audit-completion outbox worker applies the same rule to `PollInterval` and its own `MaximumCycleRetryDelay`, which defaults to five minutes. This keeps a failing database or destination from absorbing one attempt per interval indefinitely and from filling the log with an entry per cycle. `MaximumCycleRetryDelay` governs the worker loop only; the outbox's separate `BaseRetryDelay`, `MaxRetryDelay`, and `MaxRetryAttempts` settings still govern per-entry delivery retries.

`AddApplicationAuditReconciliationCore` registers the on-demand service without starting a hosted worker. This is useful for console jobs, controlled maintenance windows, or applications that already own scheduling.

Set `Enabled` to `false` to disable reconciliation completely. Set `RunWorker` to `false` to retain on-demand queries, health evaluation, and remediation APIs without running the scheduled loop.

## On-demand reconciliation

Resolve `IApplicationAuditReconciler` from a scope and call:

```csharp
ApplicationAuditReconciliationSummary summary =
    await reconciler.ReconcileAsync(cancellationToken);
```

A run compares retained audit batches and completion rows by `MutationBatchId`, verifies record counts and canonical manifest hashes, examines correlation consistency, and classifies delivery failures. The operation writes only minimized findings. It does not copy audited key, original, or current values into findings, health responses, or metrics.

## Stable finding categories

The following stable reason codes are intended for dashboards, alert routing, and recovery automation:

| Reason code | Typical severity | Meaning |
| --- | --- | --- |
| `MissingCompletion` | Critical | A retained mutation batch has no completion/outbox record after the grace period. |
| `MissingAuditBatch` | Critical | A completion record references no locally retained audit batch. This may also indicate an external-storage configuration that needs host-specific reconciliation. |
| `AuditRecordCountMismatch` | Critical | The completion receipt count differs from retained records. |
| `ManifestVerificationFailed` | Critical | The canonical retained manifest does not match the stored digest. |
| `IncompleteGeneratedValues` | Error | An added record does not contain completed generated key evidence. |
| `MalformedCorrelation` | Warning or Error | Required batch identity is missing or optional correlation values conflict within a batch. |
| `DuplicateCompletion` | Critical | More than one completion row exists for the same batch and destination. |
| `StalePending` | Warning | Pending or deferred delivery exceeded the configured threshold. |
| `StaleRetryReady` | Error | Retryable work has remained ready beyond the configured threshold. |
| `DeliveryFailed` | Error | Delivery entered a terminal failure state. |
| `DeadLettered` | Critical | Retry policy was exhausted and explicit operator action is required. |

Finding keys are deterministic for a reason, mutation batch, and destination. Repeated runs update observation timestamps rather than creating unbounded duplicate findings.

## Health checks

Registration adds `application-audit-integrity` with the tags `audit` and `integrity`. The `/health/audit-integrity` endpoint runs the check, and `/health` includes it with every other registered check. It is deliberately not tagged `ready`: an integrity finding needs operator review, and a readiness failure would take every replica out of load balancing at the same time. Alert on `/health/audit-integrity` rather than routing traffic on it.

The check returns:

- **Healthy** when no open finding crosses configured thresholds and reconciliation has run recently.
- **Degraded** when warning-level findings, stale delivery, or dead letters require attention, or when reconciliation has not completed a run in this process or its last successful run is older than `HealthStaleRunThreshold`.
- **Unhealthy** when a critical finding or manifest verification failure exists, or the open-finding threshold is reached. This takes precedence over staleness.

`HealthStaleRunThreshold` defaults to three times `Interval` (15 minutes with the default 5-minute interval) and must be greater than `Interval`. The last successful run time is tracked in the process that runs the scheduled loop, so freshness is evaluated only when `RunWorker` is `true`. With `RunWorker` set to `false`, the check reports `reconciliationFreshnessTracked: false` and relies on the external scheduler's own monitoring. Because the worker runs immediately at startup, a freshly started instance reports Degraded only until its first successful run.

Health data is minimized to counts, ages, and timestamps. It never exposes audited values or unrestricted exception text.

## Metrics and dashboard panels

NCAT publishes observable instruments through the `ProjectTemplate.AuditReconciliation` meter:

- `ncat.audit.reconciliation.findings.open`
- `ncat.audit.reconciliation.findings.error`
- `ncat.audit.reconciliation.findings.critical`
- `ncat.audit.reconciliation.manifest_failures`
- `ncat.audit.reconciliation.missing_completions`
- `ncat.audit.reconciliation.stale_delivery`
- `ncat.audit.reconciliation.dead_letters`
- `ncat.audit.outbox.pending`
- `ncat.audit.outbox.oldest_pending_age_seconds`
- `ncat.audit.outbox.retry_count`

A useful dashboard places open/critical findings and manifest failures beside outbox backlog, oldest age, retry count, and dead letters. Graph rates and duration trends in addition to current values so a slowly growing backlog is visible before it crosses a hard threshold.

## Suggested alert thresholds

Starting thresholds should be adapted to transaction volume and delivery service-level objectives:

| Signal | Suggested initial alert |
| --- | --- |
| Manifest verification failures | Immediate critical alert when greater than zero. |
| Missing completions | Critical after the configured completion grace period. |
| Dead letters | Immediate operator alert when greater than zero. |
| Oldest pending age | Warning at the delivery SLO; critical at twice the SLO. |
| Retry count | Warning on sustained growth over multiple intervals. |
| Open findings | Warning at one; critical at the configured unhealthy threshold. |

Avoid alerts based only on total historical retry count. Combine retry growth with backlog age and current ready work.

## Recovery runbook

1. **Preserve the evidence.** Do not edit or delete the authoritative `AuditRecord` rows, completion rows, findings, or prior remediation records.
2. **Stop unsafe propagation.** Pause the affected destination when a digest mismatch, duplicate completion, or unexplained missing batch could cause downstream consumers to accept inconsistent evidence.
3. **Identify scope.** Use the finding reason, `MutationBatchId`, destination, first/last observation timestamps, and correlated host logs. Do not place raw audited values into tickets or metrics.
4. **Verify independently.** Rebuild the canonical manifest from retained records and compare count, algorithm, schema version, and digest. Confirm whether audit storage is local or host-managed externally.
5. **Correct only documented mutable state.** Delivery status may be requeued under explicit operator control after the destination is corrected. Authoritative audit values and receipt identity must not be rewritten to make verification pass.
6. **Append remediation evidence.** Call `RecordRemediationAsync` with a stable action code, operator/service identity, and a bounded ticket or evidence reference. Set `ResolveFinding` only after independent verification.
7. **Run reconciliation again.** Confirm the finding resolves or remains explicitly acknowledged. Validate health and metric recovery over at least one additional interval.

Example remediation evidence:

```csharp
await reconciler.RecordRemediationAsync(
    findingId,
    new ApplicationAuditReconciliationRemediationRequest(
        ActionCode: "DestinationCorrectedAndRequeued",
        ActorId: "operations-service",
        EvidenceReference: "incident-2026-0042",
        ResolveFinding: true),
    cancellationToken);
```

Remediation records are append-only. Resolving a finding updates only its operational remediation state and timestamp; a later recurrence reopens the deterministic finding.

## Evidence-preserving boundaries

Reconciliation deliberately avoids silent repair. It does not regenerate missing audit records, replace hashes, alter record counts, or delete conflicting evidence. A host may provide a controlled, separately authorized recovery operation, but that operation should append evidence and preserve the original failure state for investigation.

Applications that store audit evidence outside the local database should supply host-specific reconciliation around that store. NCAT remains fully usable without such a component; local findings will clearly identify relationships that cannot be verified from the configured evidence source.
