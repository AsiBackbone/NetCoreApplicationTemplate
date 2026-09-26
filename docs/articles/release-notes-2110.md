# NetCoreApplicationTemplate 2.11.0 Release Notes

Release date: 2026-09-26

NetCoreApplicationTemplate 2.11.0 publishes the first immutable release baseline
for the audit-completion v1 contract and consolidates reliability and security
changes made after 2.10.0. The package ID, template identity, short name,
supported template options, and `net10.0` target are unchanged.

## Audit-completion contract

The tagged source includes the versioned vectors and compatibility policy under
`contracts/audit-completion/`. The vectors are produced through the same
manifest, hashing, outbox-staging, and dispatch code used by generated
applications, and contract tests detect production drift.

The contract directory is repository integration evidence. It remains excluded
from the `NetCoreApplicationTemplate` template package and from generated
projects. External adapters should pin `v2.11.0` and the `v1` contract path
rather than a moving branch or an unversioned copy.

## Generated-application behavior changes

- MVC applies `AutoValidateAntiforgeryTokenAttribute` globally. Every unsafe
  `POST`, `PUT`, `PATCH`, and `DELETE` action now requires a valid antiforgery
  token unless the action deliberately opts out.
- Audit value canonicalization renders `byte[]` as hexadecimal, collections as
  JSON arrays of canonical elements, and `DateTime` and `DateTimeOffset` values
  with the round-trip (`O`) format. `Hash`, `HmacSha256`, and `Truncate` output
  can therefore differ for those value types. String and numeric output is
  unchanged.
- Claims transformation returns a new principal and consistently configures
  the normalized `application:name`, `application:role`, and
  `application:subject` claims. Audit actor attribution now prefers the
  normalized subject instead of falling back to a remote IP when original
  claims are removed.
- Audit reconciliation writes run findings in one transaction inside the
  execution strategy, participates in a caller-owned transaction, guards
  updates with `ConcurrencyStamp`, and fails the whole run on a concurrent
  write instead of accepting partial or last-writer-wins results.
- Malformed audit-record discovery is ordered newest-first and bounded by
  `MaximumMalformedRecordsPerRun` (default `1000`, valid range `1` through
  `10000`) instead of loading every record without a mutation batch identifier.

## Upgrade actions

Existing generated applications are independent copies and are not modified by
installing the newer template package. When porting the 2.11.0 changes into an
existing application:

1. Exercise every unsafe MVC endpoint. Supply antiforgery tokens for browser
   form requests. Apply `[IgnoreAntiforgeryToken]` only to endpoints whose
   authentication and request-forgery boundary has been explicitly reviewed,
   such as token-authenticated APIs that do not use cookies.
2. Identify integrations, tests, reconciliation jobs, or persisted evidence
   that compare canonical audit values or their digests. Recompute expected
   values for binary, collection, and date/time inputs; plan an application-
   specific migration if historical and newly generated digests must compare.
3. Verify custom authorization and audit integrations against the normalized
   application claim types, especially when `RemoveOriginalClaims` is enabled.
4. Review reconciliation retry and transaction handling for callers that supply
   their own transaction, and configure `MaximumMalformedRecordsPerRun` for the
   deployment's recovery workload.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, all six authentication/database template combinations,
Windows/Linux/macOS smoke tests, scaffold-manifest and overlay checks,
documentation and link validation, package metadata checks, CodeQL, dependency
review, and dependency scanning. Stable publication occurs only from the
protected `v2.11.0` tag after the preparation commit is merged to `main`.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [Template packaging](template-packaging.md)
- [Audit accountability integration](audit-accountability-integration.md)
- [Production deployment checklist](production-deployment-checklist.md)
- [Container release publishing](container-publish.md)
