# Changelog

All notable changes to this project are documented in this file.

This project follows Semantic Versioning using the format `MAJOR.MINOR.PATCH`.

## Unreleased

### Added

* Added durable, release-attached SBOM, provenance, digest, checksum, and
  verification evidence for the exact NuGet package and OCI image.
* Added a fail-closed release evidence manifest and automated asset validation.
* Added a dated decision record for deferred NuGet package signing with an
  assigned owner, mandatory review date, and re-evaluation criteria.

### Changed

* Enabled `TreatWarningsAsErrors` and `CodeAnalysisTreatWarningsAsErrors` in
  `Directory.Build.props` for the repository and generated projects, so
  compiler, analyzer, and code-style warnings now fail the build.
* Distinguished permanent GitHub Release evidence from temporary Actions
  artifacts and separated NuGet publishing identity from OCI image signing.
* Backfilled release `2.9.0` from its retained original container evidence and
  exact public NuGet package, with regenerated evidence explicitly labeled.
* **Behavior change for adopters:** applications that add inline `style` attributes or `<style>` blocks to their own views will have those styles blocked by the browser after upgrading. Either move the styles into static stylesheets (preferred), use nonce- or hash-based `style-src` sources, or restore the previous allowance through configuration:

  ```json
  "ProjectTemplate": {
    "SecurityHeaders": {
      "ContentSecurityPolicy": "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';"
    }
  }
  ```

  Setting a value in `appsettings.json` or an environment-specific settings file overrides the code default. JavaScript that sets styles through the CSSOM (for example `element.style.display`) is not affected by `style-src`.

* **Behavior change for adopters:** in deployments where `RemoteIpAddress` is unavailable (for example, some Unix domain socket or in-process hosting arrangements), all unresolved clients now share the global and fixed-window permits. Heavy traffic can produce `429` responses across those clients. Resolve client identity through trusted forwarded headers, or, only when an upstream gateway already enforces limits, restore the previous behavior:

  ```json
  "ProjectTemplate": {
    "RateLimiting": {
      "UseSharedUnknownClientPartition": false
    }
  }
  ```

* Callers of `RecordRemediationAsync` should handle `DbUpdateConcurrencyException` by reloading the finding and deciding whether to retry.
* **Behavior change for API clients:** `429 Too Many Requests` responses for API-shaped requests are now `application/problem+json` Problem Details written through `IProblemDetailsService`, consistent with other error responses. The body contains `type`, `title`, `status`, `detail`, `instance`, `traceId`, `requestId`, and `correlationId` (plus `spanId` when an activity is present). The previous `{ "error", "statusCode", "traceId" }` shape is no longer returned. Clients that parsed `error` or `statusCode` should read `title`, `detail`, or `status` instead.
* Requests that are not API-shaped now receive a short `text/plain` rejection body instead of JSON. `HEAD` requests receive no body. Rejections intentionally do not re-execute the browser error page, so rejected requests stay inexpensive under load.
* `Retry-After` continues to be sent when the limiter reports a retry interval.
* **Behavior change for adopters with custom authentication schemes:** a custom external provider must be registered with a display name to appear on the login page and be challengeable through `/External/Challenge`. A displayed scheme configured as the default authenticate, sign-in, or sign-out scheme is treated as a local scheme and is no longer challengeable.
* Security-critical coverage floors are now stricter than the repository gate. `defaultMinimumLineCoverage` is raised from 60% to 75% and `defaultMinimumBranchCoverage` from 40% to 60%. Explicit per-file floors below those values were raised to match: `PersistenceTimestamp.cs` line 60% to 75%, and branch floors of 50% to 60% for `ProblemDetailsRequestClassifier.cs`, `PersistenceStringCanonicalizer.cs`, `PersistenceStringComparisonNormalizer.cs`, and `PersistenceTimestamp.cs`.
* `Assert-SecurityCriticalCoverage.ps1` accepts `-RepositoryLineCoverageThreshold`. CI passes `COVERAGE_THRESHOLD`, and the script fails when any effective line floor is below the repository gate or any per-file branch floor is below the default.
* Added `ExternalAuthenticationProviderSchemes.cs` and `DataProtectionServiceExtensions.cs` to the security-critical coverage list.
* **Behavior change for adopters enabling OIDC:** applications that call downstream APIs with provider tokens must set `SaveTokens` to `true` explicitly. The provider is disabled in the default scaffold, so no generated application changes behavior without that step.
* The audit-completion outbox and audit reconciliation workers now back off after consecutive failures. The delay doubles for each additional consecutive failure, capped by the new `MaximumCycleRetryDelay` option (five minutes for the outbox, thirty minutes for reconciliation), and returns to the normal interval after a success. Failure log entries (`19100`, `19110`) now report `ConsecutiveFailureCount` and `RetryDelaySeconds`. Per-entry delivery retries are unchanged and still governed by `BaseRetryDelay`, `MaxRetryDelay`, and `MaxRetryAttempts`.
* Startup validation rejects a `MaximumCycleRetryDelay` shorter than the worker's interval.
* CI Harden-Runner steps now read their policy from the `EGRESS_POLICY` workflow variable, which still defaults to `audit`, and carry the allow-lists each job is expected to need. A `workflow_dispatch` input runs the workflow in `block` mode so the lists can be trialed before the default changes.
* Documented `Strict-Transport-Security` as an intentional middleware omission, named the layer that owns HSTS, and added `max-age`, `includeSubDomains`, `preload`, and rollback decisions to the production deployment checklist.
* Relocated community, governance, maintainer, support, release, and asset-notice documents to `.github/` and consolidated overlapping community and maintainer files. Content is unchanged; GitHub resolves community health files from `.github/` identically to the repository root.
* Reordered the README so installation commands and the default security posture precede project goals, and consolidated the AsiBackbone boundary and documentation-ownership sections into a single related-projects block.

### Fixed

* `ApplicationAuditReconciler.RecordRemediationAsync` now records a remediation atomically. The finding status update and the remediation insert run in one transaction inside the EF Core execution strategy, so a failure in either statement leaves neither written. When the caller already owns an EF Core transaction, the method joins it and leaves commit or rollback to the caller.
* `RecordRemediationAsync` now guards the finding update with the `ConcurrencyStamp` that was read. If a reconciliation run or another remediation changes the finding first, the method throws `DbUpdateConcurrencyException`, writes nothing, and the caller can reload and retry. Previously the later write silently overwrote the earlier one.
* Remediation request fields (`ActionCode`, `ActorId`) are now validated before any database work begins.
* The rate-limit rejection log entry now uses its reserved event ID `ApplicationLogEventIds.RateLimitRejectedRequest` (`6100`). It previously used a literal `6001`, which collided with `StatusCodePageRoutedToErrorPage`.
* The unknown-client fallback warning now uses the named constant `ApplicationLogEventIds.RateLimitClientPartitionFallback`; its value remains `6002`.

### Security

* Tightened the default `Content-Security-Policy` by removing `'unsafe-inline'` from `style-src`. The default policy is now:

  ```text
  default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self';
  ```

  The change applies to the `ApplicationSecurityHeadersOptions` code default, the repository `appsettings.json`, and the generated template `appsettings.json`. The template's own views and pages contain no inline `style` attributes or `<style>` blocks.

* Rate limiting now fails closed for clients without a resolved `RemoteIpAddress`. `UseSharedUnknownClientPartition` defaults to `true` in the options code default, the repository `appsettings.json`, and the generated template `appsettings.json`, so unresolved clients share one rate-limited fallback partition. Previously each unresolved request received its own partition, which effectively left those clients unlimited.
* The unknown-client fallback warning (event ID `6002`) is now written at most once per minute and includes `SuppressedWarningCount`. Previously a deployment that never resolved client IP addresses wrote one warning per request.
* Rate-limit rejection log entries (event ID `6100`) no longer record the client IP address unless `ProjectTemplate:RequestLogging:IncludeRemoteIpAddress` is `true`. Previously the rejection log wrote the address unconditionally, contradicting the privacy default applied to request logs.
* Error-page log entries (event IDs `6000` and `6001`) now follow the same rule. Both previously recorded the remote IP address unconditionally.
* Added `RequestLoggingPrivacy.GetLoggableRemoteIpAddress` so diagnostic log entries written outside the request-logging middleware apply `IncludeRemoteIpAddress` consistently.
* `/External/Challenge` now accepts only schemes that the login page offers. A scheme is selectable when it has a display name and is not the cookie session scheme or the default authenticate, sign-in, or sign-out scheme. Previously any registered scheme other than `Cookies` could be challenged, including schemes registered without a display name such as bearer-token or test schemes. The login page and challenge endpoint now share one rule in `ExternalAuthenticationProviderSchemes`.
* Data Protection key-ring files can now be encrypted at rest with a certificate. Set `ProjectTemplate:DataProtection:KeyEncryptionCertificatePath` to a PKCS#12 certificate that includes its private key, and supply `KeyEncryptionCertificatePassword` from a secret store. The same certificate is registered for decryption so encrypted key rings work on Linux and macOS without a certificate store.
* Startup fails when the key-encryption certificate is missing, has no private key, or when a certificate password is configured without a certificate path.
* Outside Development, startup now warns when the Data Protection key-ring path is relative to the content root (event `1003`) and when no key-encryption certificate is configured (event `1004`).
* The base `appsettings.json` no longer ships permissive development values. `AllowedHosts: "*"` moved to `appsettings.Development.json`, and the LocalDB `ApplicationSqlServer` connection string with `TrustServerCertificate=True` moved there as well. The base file keeps an encrypted placeholder (`Encrypt=True;TrustServerCertificate=False`) so the SQL Server scaffold still starts.
* Outside Development, startup now warns (event `1005`) when `AllowedHosts` is absent or set to `*`, because ASP.NET Core host filtering then accepts any `Host` header.
* `ProjectTemplate:Authentication:Providers:OpenIdConnect:SaveTokens` now defaults to `false`. Saved tokens travel with the authentication cookie on every request, grow the cookie, and keep access, identity, and refresh tokens on the client.

### Documentation

* Updated the security header contract table, configuration example, and expected response header sample in `docs/articles/security-headers.md` to reflect the tightened default policy.
* Rewrote the unknown-client fallback section of `docs/articles/rate-limiting.md` to describe the shared default, the risk of each mode, and warning throttling.
* Updated `docs/articles/rate-limiting.md` with the Problem Details and plain-text rejection shapes, the reasoning for not rendering the error page, and rejection log privacy.
* Updated `docs/articles/error-handling.md` to remove `429` from the error-page status list, explain the rate-limit exception, and correct the example log line (`TraceIdentifier`, null remote IP address by default).
* Updated `docs/articles/logging.md` to state that `IncludeRemoteIpAddress` also governs rate-limit and error-page log entries, and removed a duplicated `IncludeRemoteIpAddress` key from the configuration example.
* Added key-ring encryption, certificate constraints, and startup posture warnings to `docs/articles/deployment.md`, with a production checklist item.
* Documented the security-critical floor rules and the repository-gate check in `docs/articles/build-quality.md`.
* Documented the moved development values and the `1005` warning in `docs/articles/configuration.md`.
* Documented the `SaveTokens` trade-off in `docs/articles/authentication-hardening.md` and updated the example in `docs/articles/authentication.md`.
* Documented worker backoff in `docs/articles/audit-reconciliation.md`.
* Added a Runner Egress Policy section to `docs/articles/build-quality.md` describing the trial procedure for block mode.

### Tests

* Updated `DefaultSecurityHeaders_AreApplied` to expect the tightened policy.
* Added `SecurityHeadersOptions_CodeDefault_UsesStrictContentSecurityPolicy` and `SecurityHeadersOptions_MissingConfigurationSection_ResolvesCodeDefaultContentSecurityPolicy` so the code default is verified independently of `appsettings.json`.
* Removed the unused `FindOptionsValidationException` helper from `SecurityHeadersTests`.
* Added remediation tests for insert-failure rollback, concurrent modification, caller-owned transaction joining, and request validation before writes.
* Added rate-limiting tests for the shared fallback default, explicit per-request mode, the options code default, warning throttling with suppressed counts, and throttle argument validation. The configuration binding test now binds `false` to prove configuration overrides the default.
* Replaced the JSON rejection-shape test with `RejectedRequest_ApiShaped_ReturnsProblemDetails` and added `RejectedRequest_BrowserShaped_ReturnsPlainTextWithoutRenderingErrorPage`.
* Added rejection log tests confirming the remote IP address is omitted by default and included only when request logging opts in.
* Added `RequestLoggingPrivacyTests` covering the default, opt-in, unregistered-options, and missing-request-services cases.
* Added external challenge tests for schemes without a display name, a displayed default sign-in scheme, and case-mismatched provider names, plus a login page test confirming non-challengeable schemes are not rendered.
* Added Data Protection tests for certificate-encrypted key rings with cross-instance round trip, a missing certificate file, and a password without a certificate path.
* Added startup posture tests for Data Protection warnings in Production, Staging, and Development. Existing posture tests supply a compliant Data Protection configuration so each observes only its own warning.
* Added startup posture tests for wildcard, absent, and Development `AllowedHosts`.
* Added `BackgroundServiceRetryDelayTests` covering the normal interval, doubling, the cap, a maximum below the interval, and invalid arguments.

## 2.9.0 - 2026-09-11

### Added

* Added a complete CI matrix for all six supported `authProvider` and
  `dbProvider` combinations, including option-specific scaffold assertions,
  locked restore, Release build, and generated test execution.
* Added a repository security profile documenting the effective v2.x
  solo-maintainer controls, protected publishing boundaries, emergency bypass
  procedure, secret-pattern policy, and transition criteria for additional
  maintainers.
* Added `CODE_OF_CONDUCT.md` and `GOVERNANCE.md`, with community standards,
  project roles, decision authority, release responsibility, and succession
  expectations linked from the repository's contribution surfaces.
* Added repository-level assertions that authentication, fallback authorization,
  database-provider configuration, connection-string selection, and migration
  content match each generated template option combination.

### Changed

* Updated `Microsoft.SourceLink.GitHub` from `10.0.301` to `10.0.303` and
  refreshed affected lock files.
* Updated the pinned .NET SDK container image to `10.0.401` and refreshed the
  pinned ASP.NET Core runtime image digest.
* Aligned OWASP Dependency-Check with the repository's `global.json` SDK policy.
* Reconciled `main` protection, secret scanning, push protection, publishing
  environment, CODEOWNERS, and short-lived branch-retention guidance with the
  repository's current v2.x operating model.
* Made the published documentation site the canonical package project URL and
  aligned repository, package, support, and contribution links with the
  AsiBackbone organization namespace.
* Reorganized documentation ownership and navigation so current implementation,
  operations, maintainer evidence, and historical material are clearly
  separated while existing URLs remain available.
* Updated pinned GitHub Pages deployment dependencies and scanner annotations.
* Updated release, package, citation, documentation, Kubernetes, and
  template-packaging metadata for release `2.9.0`.

### Fixed

* Corrected the Source Link dependency affected by CVE-2026-62900.
* Corrected generated-template exclusion rules so repository-only code of
  conduct and governance files cannot leak into consumer scaffolds.
* Corrected documentation and workflow paths reported by code scanning so
  findings resolve to repository-relative files.

### Security

* Updated Source Link to the patched release for CVE-2026-62900.
* Documented the fail-closed repository-host security profile and the limits of
  controls that repository-local CI cannot enforce by itself.
* Preserved protected NuGet Trusted Publishing, container publication,
  provenance, dependency review, CodeQL, and secret-scanning boundaries.

### Compatibility

* This is a backward-compatible minor release within the stable `2.x` package
  line.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template identity remains `AsiBackbone.NetCoreApplicationTemplate.CSharp`,
  the group identity remains `AsiBackbone.NetCoreApplicationTemplate`, and the
  short name remains `netcoreapp-template`.
* Supported template options and generated runtime behavior remain unchanged.
* Existing projects generated from earlier releases are not modified
  automatically.
* The target framework remains `net10.0`.

## 2.8.0 - 2026-09-06

### Added

* Added persistent ASP.NET Core Data Protection key-ring configuration with a stable application discriminator, durable Docker Compose storage, and a shared Kubernetes volume example.
* Added checked-in NuGet lock files to generated projects and locked-mode restore validation for repository, scaffold, and Docker builds.
* Added a configurable `DeprecatedVersionSunset` API-versioning option; deprecated endpoints omit the `Sunset` header until a consuming application supplies a meaningful date.
* Added complete shared-layout rendering for login, access-denied, and browser error views.
* Added template-content overlay, scaffold-reference, package-lock, and repository-lint validation to prevent generated-output drift and maintainer-only content leakage.
* Added pinned Trivy scanning for the exported release container image and automated base-image dependency tracking.

### Changed

* Moved the pinned .NET SDK feature band to `10.0.400` across repository, generated-project, container, CI, and documentation surfaces.
* Aligned the internal template identity and group identity with the `AsiBackbone` organization namespace while preserving the public package ID and template short name.
* Changed production forwarded-header validation to require explicit trusted proxy or network configuration by default when forwarded client addresses and rate limiting are enabled.
* Changed generated SQL Server scaffolds to omit SQLite-specific migration source and migration tests.
* Changed request logging to exclude user names and remote IP addresses by default and changed the built-in external-login audit policy to mask contact fields.
* Changed repository-only coverage and lint tests so they are not distributed into consumer scaffolds; generated tests now inherit each scaffold's own authentication posture.
* Added uppercase project-name replacement so environment-variable and Docker Compose identifiers no longer retain `PROJECTTEMPLATE` tokens.
* Updated GitHub Actions dependencies and pruned stale dependency-scan suppressions and unused package entries.
* Updated release, package, citation, documentation, Kubernetes, and template-packaging metadata for release `2.8.0`.

### Fixed

* Corrected Data Protection behavior across restarts and replicas by persisting keys outside the application container filesystem.
* Corrected the default authentication sign-in guidance so the cookie handler is not described as a credential-verification flow.
* Removed duplicate error-handling middleware registration and made the advertised Problem Details test endpoint routable.
* Restricted the error route to pipeline-routed requests and prevented callers from choosing arbitrary response status codes through its route value.
* Realigned template content with repository `appsettings.json` so rate-limit fallback settings reach generated projects.
* Removed the stale generated SQL migration script from repository, package, and scaffold output.
* Corrected asset license notices and ensured the consumer-facing license inventory is packaged and scaffolded.
* Removed the dangling `CITATION.cff` solution item from generated solutions.
* Removed the unused authenticated GitHub Packages source from repository restore configuration.
* Corrected CI template cleanup to uninstall by package ID and fail visibly when cleanup fails.
* Excluded SQLite database files and sidecars from recursive package inputs.

### Security

* Made persistent Data Protection keys an explicit deployment surface so authentication cookies and other protected payloads remain valid across restarts and replicas.
* Made missing production proxy trust fail fast by default instead of allowing misleading client-IP logging and rate-limit partitioning.
* Reduced default personal-data retention in audit rows and request logs.
* Enforced central transitive package pinning so the secured `System.Drawing.Common` version cannot fall back to the vulnerable `4.7.0` dependency selected by the SAML dependency chain.
* Added release-container vulnerability scanning and prevented local SQLite artifacts from entering template packages.

### Compatibility

* This is a backward-compatible minor release within the stable `2.x` package line.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`, and supported template options remain unchanged.
* The internal template identity changes to `AsiBackbone.NetCoreApplicationTemplate.CSharp`, and the group identity changes to `AsiBackbone.NetCoreApplicationTemplate`.
* Existing projects generated from earlier releases are not modified automatically.
* New production deployments that process forwarded client addresses must configure trusted proxies or networks, or deliberately opt out of strict startup validation.
* Consumers that require user-name or remote-IP request logging must now opt in explicitly and apply appropriate retention controls.
* Deprecated API versions emit a `Sunset` header only after the consuming application configures a date.

## 2.7.0 - 2026-08-30

### Added

* Added an explicit cross-repository documentation ownership contract establishing AsiBackbone/Learning as the canonical educational source while keeping NCAT authoritative for template behavior, configuration, generated output, runtime contracts, ADRs, releases, and repository-specific implementation decisions.
* Added a documentation ownership inventory and dedicated API documentation landing surface to make NCAT's implementation-focused documentation boundaries easier to navigate.
* Added automated DocFX documentation link validation with Lychee, including NCAT-to-Learning and Learning-to-NCAT guardrails, URL-continuity checks, pull-request validation, manual execution, and scheduled link-rot checks.
* Added repository-level NuGet lock-file coverage to strengthen deterministic restore and dependency reproducibility.

### Changed

* Reorganized DocFX information architecture and consolidated the top navigation around template usage, implementation, operations, API reference, and project resources.
* Refocused authentication, authorization, configuration, data-access, middleware, logging, telemetry, health-check, forwarded-header, security-header, rate-limiting, error-handling, and optional application-layer guidance on NCAT's concrete implementation, with broader architectural teaching deferred to AsiBackbone/Learning.
* Migrated the NCAT test suite from xUnit v3 to xUnit v4 and updated the associated Microsoft.NET.Test.Sdk, test dependencies, test configuration, and dependency lock files.
* Updated ASP.NET Core, OpenTelemetry, testing, and GitHub Actions dependencies while preserving the established NCAT 2.x template contract.
* Updated release, package, citation, documentation, and Kubernetes example metadata for release `2.7.0`.

### Fixed

* Corrected DocFX source-link remapping in the documentation validation workflow so generated source links resolve to the underlying Markdown files instead of being interpreted as directories.

### Security

* Added a narrowly scoped, time-bounded OWASP Dependency-Check suppression for CVE-2026-54285 when it is incorrectly associated with the .NET `OpenTelemetry.Exporter.OpenTelemetryProtocol` NuGet package; the referenced vulnerability applies to the JavaScript OpenTelemetry implementation.
* Retained normal scanning for other vulnerabilities and package versions rather than applying a broad CVE suppression.

### Compatibility

* This is a backward-compatible minor release within the stable `2.x` package line.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`.
* The internal template and template-group identities remain unchanged.
* Supported template options and the generated scaffold structure remain unchanged.
* No intentional breaking change is introduced to NCAT's public application surface or configuration contract.

## 2.6.2 - 2026-08-22

### Security

* Updated the pinned ASP.NET Core .NET 10 runtime container image to the patched .NET 10.0.11 servicing image, addressing CVE-2026-62901 detected by the release container vulnerability gate.

### Changed

* Updated release, package, citation, documentation, and Kubernetes example metadata for release `2.6.2`.

### Notes

* This is a backward-compatible security patch release.
* No NetCoreApplicationTemplate public API, template option, configuration surface, generated scaffold structure, package identity, template identity, or intended application behavior changes are introduced by this release.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`.

## 2.6.1 - 2026-08-22

### Changed

* Updated `Asp.Versioning.Mvc` from `10.0.0` to `10.2.1`, incorporating backward-compatible API versioning fixes, analyzers, and supporting improvements.
* Updated Entity Framework Core packages from `10.0.10` to `10.0.11`.
* Updated `Microsoft.Extensions.Configuration.Abstractions` from `10.0.10` to `10.0.11`.
* Updated GitHub Actions security, analysis, provenance, and workflow-hardening dependencies, including CodeQL, Harden Runner, build provenance attestation, and Zizmor tooling.
* Updated the ReportGenerator .NET tool from `5.5.10` to `5.5.11`.
* Refreshed NuGet dependency lock files for the updated dependency graph.
* Updated release, package, citation, documentation, and Kubernetes example metadata for release `2.6.1`.

### Fixed

* Normalized `.config/dotnet-tools.json` line-ending handling to prevent false-positive working-tree modifications on Windows.

### Notes

* This is a backward-compatible patch release focused on dependency servicing, build and security tooling maintenance, and repository hygiene.
* No NetCoreApplicationTemplate public API, template option, configuration surface, generated scaffold structure, package identity, template identity, or intended runtime application behavior changes are introduced by this release.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`.

## 2.6.0 - 2026-08-07

### Changed

- Migrated repository and release metadata from `cdcavell/NetCoreApplicationTemplate` to `AsiBackbone/NetCoreApplicationTemplate` following the repository transfer to the AsiBackbone organization.
- Migrated the canonical repository-maintained container image from `ghcr.io/cdcavell/netcoreapplicationtemplate` to `ghcr.io/asibackbone/netcoreapplicationtemplate`.
- Updated Kubernetes examples and container publishing documentation to use the organization-owned GHCR namespace.
- Updated the GitHub Packages source from the personal `cdcavell` namespace to the `AsiBackbone` organization namespace.
- Updated repository, package, citation, generated-scaffold, documentation, security, and deployment references for the new organization location.
- Updated GitHub CodeQL Action usage from `4.37.3` to `4.37.4`.
- Updated the Docker Login Action used by container publishing to `4.6.0`.

### Fixed

- Corrected stale repository ownership and documentation references following the repository transfer.
- Corrected generated scaffold upstream references so newly created applications point to the current repository and documentation site.

### Notes

- This is a backward-compatible minor release focused on repository and distribution ownership migration.
- No template source behavior, generated scaffold structure, NuGet package identity, template identity, configuration surface, or runtime application behavior changes are intended.
- Existing previously published images under `ghcr.io/cdcavell/netcoreapplicationtemplate` are not modified by this release.

## 2.5.0 - 2026-08-02

### Added

* Added `ProjectTemplate:DataAccess:Diagnostics` as an explicit EF Core diagnostics configuration surface.
* Added `ProjectTemplate:DataAccess:Diagnostics:EnableDetailedErrors` for opt-in EF Core detailed error diagnostics.
* Added `ProjectTemplate:DataAccess:Diagnostics:EnableEfCoreTraceBridge` for opt-in forwarding of EF Core simple logging through `ILogger<ApplicationDbContext>` at `Trace` level.
* Added dedicated EF Core diagnostics documentation covering standard EF Core logging, detailed errors, the optional trace bridge, sensitive-data logging boundaries, and recommended operational use.
* Added regression coverage verifying EF Core diagnostics behavior for both scoped `ApplicationDbContext` instances and contexts created through `IDbContextFactory<ApplicationDbContext>`.

### Changed

* Changed EF Core detailed errors from always enabled to explicitly opt-in, with `EnableDetailedErrors` defaulting to `false`.
* Changed the custom EF Core `LogTo(...)` trace bridge from always enabled to explicitly opt-in, with `EnableEfCoreTraceBridge` defaulting to `false`.
* Moved optional EF Core diagnostics configuration into the infrastructure data-access registration so scoped and factory-created contexts receive the same settings.
* Preserved the `ApplicationSaveChangesInterceptor` independently of optional diagnostic settings so auditing, canonicalization, mutation manifests, and other save-pipeline behavior remain active when diagnostics are disabled.
* Preserved EF Core's standard `Microsoft.Extensions.Logging` integration as the normal production logging path.
* Updated OpenTelemetry ASP.NET Core and HTTP instrumentation to `1.17.0`.
* Updated `SQLitePCLRaw.bundle_e_sqlite3` to `3.0.4`.
* Updated GitHub Actions dependencies including checkout, .NET setup, CodeQL, OpenSSF Scorecard, Zizmor, and Docker registry authentication tooling.
* Refreshed NuGet dependency lock files for the updated dependency graph.
* Updated repository, package, template-packaging, citation, Zenodo, and Kubernetes example metadata for release `2.5.0`.

### Security

* Hardened the Kubernetes deployment example with a `RuntimeDefault` seccomp profile.
* Configured the Kubernetes workload to run as a non-root application-specific UID/GID (`10001`).
* Configured the Kubernetes container to drop all Linux capabilities.
* Enabled a read-only container root filesystem while retaining explicitly writable data and logging volumes.
* Added CPU and memory requests and limits to the Kubernetes example.
* Disabled automatic Kubernetes service-account token mounting because the sample application does not require Kubernetes API access.
* Changed the Kubernetes example to always check the registry for its version-pinned image on pod startup.
* Kept EF Core sensitive-data logging disabled when either of the new diagnostic options is enabled; detailed errors and the trace bridge do not implicitly enable `EnableSensitiveDataLogging()`.

### Compatibility

* This is a backward-compatible minor release within the stable `2.x` package line.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`.
* The internal template and template-group identities remain unchanged.
* Supported template options remain unchanged.
* Existing projects generated from earlier releases are not modified automatically.
* Both new EF Core diagnostic settings default to `false` when omitted.
* Applications that do not opt into the new diagnostics retain normal EF Core `Microsoft.Extensions.Logging` behavior without the additional detailed-error or `LogTo(...)` diagnostic pipelines.
* The existing save-changes interceptor and persistence pipeline remain active regardless of the optional diagnostics configuration.


## 2.4.0 - 2026-07-20

### Added

* Added a framework-neutral audit accountability context for propagating actor, request, operation, retry-attempt, decision, tenant, organization, correlation, and distributed-tracing identifiers into EF Core mutation audit records.
* Added mutation batch identifiers and minimized mutation audit receipts so host applications can correlate persisted changes with external workflow, archive, SIEM, or governance records without copying audited entity values.
* Added host-replaceable audit value protection supporting include, mask, hash, omit, and truncate dispositions before audit values are persisted or included in canonical manifests.
* Added versioned, privacy-safe canonical mutation manifests with deterministic ordering, SHA-256 hashing, and independent retained-batch verification contracts.
* Added opt-in audited transaction coordination for atomically persisting business mutations, NCAT audit records, generated-value completion, mutation receipts, and optional database-local completion handoffs.
* Added support for joining existing EF Core transactions through savepoints while preserving explicit transaction ownership and rollback behavior.
* Added an opt-in, provider-neutral durable audit-completion outbox with stable idempotency keys, bounded retries, deferred delivery, terminal failure, and dead-letter handling.
* Added publisher adapter contracts and hosted dispatch support for delivering minimized mutation-completion receipts after the originating database transaction commits.
* Added audit-reconciliation services that compare retained audit batches with completion records and detect missing relationships, count mismatches, canonical manifest failures, malformed correlation, duplicate completion, stalled delivery, terminal failure, and dead letters.
* Added durable, minimized reconciliation findings with stable reason codes, severities, remediation states, and append-only remediation evidence.
* Added audit-integrity health checks and provider-neutral metrics for reconciliation findings, manifest failures, missing completions, delivery backlog, pending age, retries, and dead letters.
* Added forwarded-header trust startup diagnostics for deployments using forwarded client addresses with client-IP rate limiting.
* Added an optional `ProjectTemplate:ForwardedHeaders:RequireExplicitProxyTrust` setting that fails startup outside Development when forwarded client-IP processing is enabled without a configured trusted proxy or network.
* Added property-based tests for persistence string normalization and canonicalization invariants.
* Added NuGet dependency lock files and expanded locked-restore coverage.
* Added OpenSSF Scorecard, OpenSSF Best Practices, workflow-security, and dependency-scanning improvements.

### Changed

* Changed the default scaffold authorization posture so routed endpoints without authorization metadata require an authenticated user.
* Added `ProjectTemplate:Authorization:RequireAuthenticatedUserByDefault` and coordinated the setting with template authentication options.
* Changed `--authProvider none` into an explicit architectural opt-out that disables application authentication, cookie authentication, and the authenticated fallback authorization policy.
* Standardized authentication and authorization terminology throughout repository, package, generated-consumer, and DocFX documentation.
* Changed the local authentication cookie default from `CookieSecurePolicy.SameAsRequest` to `CookieSecurePolicy.Always`.
* Added an explicit `ProjectTemplate:Authentication:Cookie:AllowInsecureHttp` override for local Development and reject that override in all other environments.
* Isolated mutable SaveChanges audit state and completed mutation receipts by `ApplicationDbContext` instance.
* Added `IApplicationMutationAuditReceiptRegistry` for explicit receipt lookup when multiple factory-created contexts exist in one dependency-injection scope.
* Updated ASP.NET Core, Entity Framework Core, testing, observability, and supporting Microsoft package dependencies.
* Strengthened GitHub Actions token permissions, immutable action pinning, runner monitoring, dependency review, CodeQL, OWASP Dependency-Check, and release-evidence workflows.
* Expanded generated-template, migration, authorization, authentication, audit, Docker, and cross-platform smoke-test coverage.
* Updated repository, package, template-packaging, citation, and Zenodo metadata for release `2.4.0`.

### Fixed

* Fixed audit-state leakage risk when multiple `ApplicationDbContext` instances are used within the same dependency-injection scope.
* Fixed authentication cookies potentially lacking the `Secure` attribute when proxy or request-scheme configuration is incomplete.
* Fixed newly introduced routed endpoints being unintentionally public when authorization metadata was omitted.
* Fixed the risk of misleading client-IP logging and shared proxy rate-limit partitions by surfacing missing forwarded-header trust configuration.
* Removed obsolete controller and empty test placeholders from the generated template source surface.
* Corrected reviewed OWASP Dependency-Check cross-ecosystem false-positive handling while retaining narrowly scoped, time-bounded suppressions.

### Security

* Default generated applications now use a closed-by-default routed-endpoint authorization posture.
* Intentionally public routed endpoints must be explicitly marked with `[AllowAnonymous]`, `.AllowAnonymous()`, or equivalent anonymous metadata.
* Authentication cookies are secure by default independently of the application-perceived request scheme.
* Forwarded client addresses continue to be accepted only from explicitly trusted proxies and networks; NCAT does not parse or trust arbitrary raw `X-Forwarded-For` values.
* Audit completion, outbox, reconciliation, health, and metric surfaces retain only minimized identifiers, counts, hashes, state, timestamps, and bounded diagnostics rather than unrestricted audited values.
* Canonical manifest hashes prove correspondence with a retained protected-value batch but do not claim immutable storage, actor authenticity, legal compliance, transaction durability, or exactly-once delivery.

### Compatibility

* This is a backward-compatible minor release within the stable `2.x` package line.
* The public NuGet package ID remains `NetCoreApplicationTemplate`.
* The template short name remains `netcoreapp-template`.
* The internal template and template-group identities remain unchanged.
* Existing projects generated from earlier releases are not modified automatically.
* Audit transaction coordination, completion outbox, reconciliation workers, and strict forwarded-header trust validation remain opt-in.
* Applications that do not enable the new audit capabilities retain the existing direct `SaveChanges` and `SaveChangesAsync` paths.
* NCAT remains independent of AsiBackbone and does not require an external governance, archive, SIEM, or audit product.

## 2.3.1 - 2026-07-06

### Changed

* Changed centralized Problem Details exception mapping so plain `ArgumentException` is treated as an internal server fault instead of a bad request.
* Preserved `BadHttpRequestException` mapping to HTTP 400 for request-level malformed input failures.
* Updated Problem Details tests to verify status, title, and production detail-hiding behavior for both request-level and internal exception paths.
* Updated release metadata, package README examples, template packaging docs, citation metadata, and Zenodo metadata for `2.3.1`.

### Notes

* This is a patch release because it hardens exception classification and diagnostics without changing package identity, template identity, template options, or the default scaffold purpose.
* Internal/developer argument failures now contribute to server-error diagnostics instead of being misclassified as client bad-request traffic.

## 2.3.0 - 2026-07-03

### Added

* Added `UseSharedUnknownClientPartition` to make shared unknown-client rate-limit partitioning an explicit opt-in behavior.
* Added `UnknownClientPartitionKey` so the unresolved-client fallback partition key can be configured.
* Added warning logging when client IP partitioning falls back because `HttpContext.Connection.RemoteIpAddress` is unavailable.
* Added tests covering resolved client IP partitioning, default per-request fallback partitioning, explicit shared fallback partitioning, fallback warning logging, option binding, and fallback-key validation.

### Changed

* Changed unresolved-client rate-limit fallback behavior from a silent shared `"unknown-client"` bucket to a per-request fallback partition by default.
* Preserved client rate-limit partitioning against `HttpContext.Connection.RemoteIpAddress` rather than parsing raw `X-Forwarded-For` headers inside the rate limiter.
* Updated rate-limiting documentation to cover fallback partition behavior, production tuning, and forwarded-header trust requirements.
* Updated forwarded-header documentation to emphasize `KnownProxies` / `KnownNetworks`, middleware ordering, and the risk of trusting raw forwarded headers directly.
* Updated release metadata, package README examples, template packaging docs, citation metadata, and Zenodo metadata for `2.3.0`.

### Notes

* This is a minor release because it adds a new rate-limit configuration surface and changes unresolved-client fallback behavior while preserving the stable `2.x` package identity, template short name, template options, and default scaffold purpose.
* Production deployments behind proxies, load balancers, ingress controllers, CDNs, or gateways should verify forwarded-header trust configuration so rate limiting and request logging see the corrected client IP address.
* Set `ProjectTemplate:RateLimiting:UseSharedUnknownClientPartition` to `true` only when unresolved clients should intentionally share the configured unknown-client fallback bucket.

## 2.2.0 - 2026-06-29

### Added

* Added `IApplicationSaveChangesPipeline` and `ApplicationSaveChangesPipeline` to move EF Core save preparation out of `ApplicationDbContext` and into an application-owned persistence pipeline.
* Added `ApplicationSaveChangesInterceptor` as the composite EF Core save lifecycle interceptor for invoking the save pipeline through `SavingChanges` / `SavingChangesAsync` and `SavedChanges` / `SavedChangesAsync` hooks.
* Added EF Core save pipeline documentation covering default pipeline order, extension seams, audit lifecycle safety, and the decision to keep a composite interceptor by default.
* Added ADR 0004 documenting the decision to keep the composite SaveChanges interceptor until a concrete consumer or maintenance need justifies specialized interceptors.
* Added tests that verify sync and async save-pipeline invocation through `ApplicationDbContext`.
* Added branch-focused tests for `ApplicationSaveChangesInterceptor`, including constructor null-guard, non-`ApplicationDbContext`, and bounded after-save follow-up branches.

### Changed

* Reduced repeated EF Core `ChangeTracker` inspection by materializing Added/Modified/Deleted entries once and reusing that list across string canonicalization, lookup normalization, timestamp normalization, concurrency stamping, and audit entry creation.
* Reduced `ApplicationDbContext` save overrides to optimistic-concurrency exception handling around EF Core's native save flow.
* Kept `ConcurrencyStamp` as the default application-managed optimistic concurrency token and documented why that remains the portable SQLite / SQL Server baseline.
* Updated package icon and favicon image assets used for repository, NuGet package, and documentation branding.
* Updated release metadata, package README examples, template packaging docs, citation metadata, and Zenodo metadata for `2.2.0`.

### Notes

* This is a minor release because it introduces and documents a clearer EF Core save-pipeline extension seam while preserving the stable `2.x` package identity, template short name, template options, and default scaffold behavior.
* The default generated scaffold continues to use the same package identity, template identity, authentication options, data-access options, and local SQLite development path.
* SQL Server-only consumers may still replace the application-managed concurrency token with provider-native rowversion behavior when appropriate, but the template default remains provider-portable.

## 2.1.0 - 2026-06-27

### Added

* Added optional application and domain layer guidance for consumers who outgrow the default `Web` / `Infrastructure` split.
* Added production authentication hardening guidance covering provider configuration, HTTPS/proxy behavior, redirect and callback URLs, cookie security, claims translation, token handling, session behavior, and provider smoke testing.
* Added middleware ordering rationale and documented order-sensitive invariants for the centralized application pipeline.
* Added a template-owned `IApplicationAuditStore` seam for application audit records.
* Added `ProjectTemplate:DataAccess:Auditing:StorageMode` with `Local` as the built-in default and `Outbox` / `ExternalSink` as explicit extension-mode names.
* Added focused tests for default local audit store registration, audit storage mode configuration, custom audit store behavior, and sync no-op save behavior.

### Changed

* Routed synchronous and asynchronous audit record creation through the audit store seam while preserving local EF Core audit storage by default.
* Short-circuited `ApplicationDbContext.SaveChanges` and `SaveChangesAsync` when the EF Core change tracker has no pending changes.
* Refreshed configuration, data-access, public-surface, and example appsettings documentation to align with the audit storage configuration shape.
* Updated article index and documentation navigation coverage for newer guidance pages.
* Reformatted documentation image assets for consistent repository and documentation display.

### Notes

* This is a minor release because it adds optional extension points and documentation while preserving the stable `2.x` package identity, template short name, template options, and default scaffold behavior.
* Local audit storage remains the default. `Outbox` and `ExternalSink` modes require a consuming application to register a custom `IApplicationAuditStore` implementation.
* Existing consumers do not need to change configuration unless they intentionally adopt a non-local audit storage mode.

## 2.0.1 - 2026-06-26

### Changed

* Refreshed post-2.0 documentation to align README, DocFX navigation, package guidance, release guidance, support policy, maintainer guidance, security policy, telemetry notes, runtime readiness notes, and Docker documentation with the current `NetCoreApplicationTemplate` package identity.
* Replaced repository/package branding assets with an original NetCoreApplicationTemplate icon that better reflects the project as a secure, extensible ASP.NET Core application baseline.
* Updated documentation-site branding to use the new icon assets without disrupting DocFX navigation layout.
* Confirmed NuGet Trusted Publishing workflow permissions include the required GitHub Actions OIDC token permission for public package publication.

### Notes

* This is a patch release focused on documentation, package metadata, release-readiness cleanup, branding assets, and trusted-publishing readiness.
* No generated scaffold behavior, template short name, template options, or public package identity changes are included.

## 2.0.0 - 2026-06-25

### Breaking Changes

* Renamed the NuGet package from `CDCavell.NetCoreApplicationTemplate` to `NetCoreApplicationTemplate`.
* Consumers should update package installation commands and references to use the new package ID.

### Changed

* Updated the public NuGet package identity to the simplified project-centered name `NetCoreApplicationTemplate`.
* Updated package metadata to align with the new package ID.
* Updated release metadata for the `2.0.0` package line.
* Switched NuGet.org publication to **NuGet Trusted Publishing**, removing the need for a long-lived NuGet API key for public package publishing.
* Updated the package publishing workflow to use GitHub Actions OIDC authentication for NuGet.org publishing.

### Migration Notes

Replace package installation commands such as:

```powershell
dotnet new install CDCavell.NetCoreApplicationTemplate
```

with:

```powershell
dotnet new install NetCoreApplicationTemplate
```

If the old package is already installed locally, uninstall it first:

```powershell
dotnet new uninstall CDCavell.NetCoreApplicationTemplate
dotnet new install NetCoreApplicationTemplate
```

### Notes

The previous `CDCavell.NetCoreApplicationTemplate` package should be treated as the legacy package identity and deprecated on NuGet with alternate package guidance pointing to `NetCoreApplicationTemplate`.

This release does not change the project’s core purpose: providing a production-oriented ASP.NET Core application template with structured logging, security headers, forwarded headers, rate limiting, centralized error handling, authentication-ready architecture, and EF Core-ready structure.

## 1.0.4 - 2026-06-22

### Fixed

* Added `SQLitePCLRaw.bundle_e_sqlite3` package reference to replace deprecated SQLite bundle usage.
* Updated SQLite dependency path to use the supported bundled native SQLite provider.
* Resolved dependency warning related to deprecated SQLite library usage.

### Notes

* This is a dependency maintenance release.
* No application behavior, public APIs, or template structure were intentionally changed.

## 1.0.3 - 2026-06-15

### Changed

* Updated centrally managed NuGet package versions used by the template, tests, persistence layer, authentication integrations, observability support, configuration abstractions, logging configuration, and test dependencies.
* Updated Microsoft ASP.NET Core authentication and MVC testing packages from `10.0.8` to `10.0.9`.
* Updated Microsoft Entity Framework Core packages from `10.0.8` to `10.0.9`.
* Updated `Microsoft.Extensions.Configuration.Abstractions` from `10.0.8` to `10.0.9`.
* Updated OpenTelemetry hosting/exporter packages from `1.15.3` to `1.16.0`.
* Updated `Serilog.Settings.Configuration` from `10.0.0` to `10.0.1`.
* Updated `System.Drawing.Common` from `10.0.8` to `10.0.9`.

### Maintenance

* Keeps the stable `1.0.x` line current with dependency maintenance only.
* No template source code, generated scaffold structure, package identity, or documented runtime behavior changes are included in this patch release.

## 1.0.2 - 2026-06-09

### Changed

* Updated application and package metadata for the stable `1.0.x` line.
* Refreshed release documentation and package validation guidance.
* Updated README release references and template installation examples.

### Maintenance

* Prepared the repository for the `1.0.2` package release.
* No generated scaffold behavior changes are included in this release.
