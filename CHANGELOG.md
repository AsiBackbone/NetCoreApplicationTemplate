# Changelog

All notable changes to this project are documented in this file.

This project follows Semantic Versioning using the format `MAJOR.MINOR.PATCH`.

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
