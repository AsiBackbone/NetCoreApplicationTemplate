# Documentation Ownership Inventory

> **Status:** Temporary planning artifact for issue #413.  
> **Scope:** Classification only. This document does not move, delete, or rewrite the documentation it inventories.  
> **Inventory date:** 2026-08-30.

## Purpose

This inventory assigns a long-term ownership disposition to every Markdown page currently published by the NCAT DocFX site.

The publication boundary is defined by `docs/docfx.json`:

- `docs/index.md`
- `docs/articles/**/*.md`
- `docs/adr/**/*.md`

Generated API reference and test-coverage surfaces are recorded separately because they are published outputs rather than hand-authored Markdown articles.

NCAT remains authoritative for concrete generated behavior, template options, configuration, deployment and operations, public compatibility surface, repository-local ADRs, release behavior, and runtime contracts. [ASI Backbone Learning](https://asibackbone.github.io/Learning/) is authoritative for reusable architecture education, conceptual explanation, tradeoff analysis, tutorials, and organization-level teaching.

## Disposition Definitions

- **KEEP** — authoritative NCAT implementation, runtime, operational, repository, or reference documentation.
- **REFACTOR** — retain the NCAT page and URL, but narrow long-term ownership to exact NCAT behavior; move or link reusable theory to Learning.
- **REDIRECT** — retain the NCAT URL as a concise NCAT-specific pointer to a canonical Learning page.
- **MOVE/COPY-THEN-REDIRECT** — preserve the NCAT URL, establish the educational content in Learning, then replace the NCAT page with a concise implementation-specific pointer.
- **ARCHIVE** — preserve historical material in NCAT, but remove it from the main current-consumer path; retain URL continuity.
- **REMOVE** — obsolete material only after a safe inbound-link and URL-continuity plan exists.

## Inventory

| Document | Current role | Disposition | Canonical destination | NCAT replacement/redirect plan | Notes |
| --- | --- | --- | --- | --- | --- |
| `docs/index.md` | DocFX site landing page and ownership router | **KEEP** | NCAT | Keep current URL and implementation-first navigation | Correctly establishes NCAT vs Learning authority. |
| `docs/articles/index.md` | Template and operations documentation home | **KEEP** | NCAT | Keep as article landing page | Current-consumer navigation belongs with the implementation. |
| `docs/articles/template-packaging.md` | NuGet install, template generation, symbols, scaffold validation | **KEEP** | NCAT | No redirect | Exact package and generated-template behavior. |
| `docs/articles/getting-started.md` | Repository prerequisites, build, test, run, local docs | **KEEP** | NCAT | No redirect | Repository-specific onboarding. |
| `docs/articles/project-structure.md` | Generated solution/projects and responsibilities | **KEEP** | NCAT | No redirect | Describes the actual generated scaffold. |
| `docs/articles/configuration.md` | Application-owned configuration surface | **KEEP** | NCAT | No redirect | Configuration keys/defaults are runtime contract. |
| `docs/articles/middleware.md` | Concrete pipeline order plus middleware guidance | **REFACTOR** | NCAT for exact order; Learning for general middleware-ordering education | Preserve URL; retain order/invariants/extensions and link to Learning for theory | Duplicate-authority risk is general middleware ordering guidance. ADR-0002 remains NCAT rationale. |
| `docs/articles/authentication.md` | Authentication defaults, providers, endpoints, validation, claims behavior | **REFACTOR** | NCAT for exact providers/defaults; Learning for general authentication architecture | Preserve URL; keep configuration, endpoints, provider behavior, claims mapping; link general concepts outward | Exact template behavior must remain local. |
| `docs/articles/authentication-hardening.md` | Production hardening requirements for NCAT authentication | **KEEP** | NCAT | No redirect | Operator-facing, implementation-specific security guidance tied to NCAT options. |
| `docs/articles/authorization.md` | NCAT policies, fallback/default behavior, endpoint classification | **REFACTOR** | NCAT for exact policies; Learning for reusable authorization concepts | Preserve URL; keep named policies and template behavior; link general policy education outward | Learning already contains broader authorization material; avoid two canonical explanations. |
| `docs/articles/error-handling.md` | NCAT status pages, Problem Details, exception handling, request correlation | **REFACTOR** | NCAT for exact error contract; Learning for general centralized error-handling/Problem Details education | Preserve URL; retain response behavior and safety contract | General Problem Details teaching should not become authoritative in both repositories. |
| `docs/articles/logging.md` | Serilog startup/runtime/request logging contract | **REFACTOR** | NCAT for exact Serilog configuration; Learning for structured-logging concepts | Preserve URL; keep sinks/format/events/configuration and link theory outward | ADR-0001 remains the repository-local design rationale. |
| `docs/articles/telemetry.md` | NCAT OpenTelemetry traces, metrics, correlation | **REFACTOR** | NCAT for exact telemetry surface; Learning for general observability concepts | Preserve URL; retain instrumentation/configuration/export behavior | Avoid duplicating reusable observability teaching. |
| `docs/articles/security-headers.md` | NCAT security-header middleware and v1 contract | **REFACTOR** | NCAT for exact headers/options/exclusions; Learning for general browser-header guidance | Preserve URL; keep contract and tests; link general security theory outward | Header values and intentional opt-outs remain NCAT truth. |
| `docs/articles/forwarded-headers.md` | Reverse-proxy support and trust diagnostics | **REFACTOR** | NCAT for exact proxy configuration/diagnostics; Learning for general proxy-trust education | Preserve URL; retain supported options, defaults, diagnostics, and deployment implications | Generic forwarded-header trust concepts should have one educational home before any redirect is attempted. |
| `docs/articles/rate-limiting.md` | NCAT rate-limit defaults, partitioning, policies, middleware order | **REFACTOR** | NCAT for exact policies/defaults; Learning for general rate-limiting design | Preserve URL; keep partition keys, fallback behavior, configuration, tests | Production tuning principles are reusable education; concrete policy behavior is not. |
| `docs/articles/health-checks.md` | NCAT health endpoints and health-check behavior | **KEEP** | NCAT | No redirect | Primarily an implementation and operations contract. |
| `docs/articles/api-versioning.md` | NCAT API-versioning package/configuration conventions | **KEEP** | NCAT | No redirect | Template-specific public/API behavior. |
| `docs/articles/data-access.md` | EF Core/SQLite baseline, migrations, auditing, persistence behavior | **REFACTOR** | NCAT for exact persistence implementation; Learning for general EF Core/data-access education | Preserve URL; retain provider/defaults/migrations/auditing/contracts; link reusable design guidance outward | Large page with the highest duplication risk among core implementation references. |
| `docs/articles/ef-core-diagnostics.md` | NCAT EF Core diagnostic configuration | **KEEP** | NCAT | No redirect | Exact diagnostics behavior and configuration. |
| `docs/articles/ef-core-save-pipeline.md` | NCAT SaveChanges/interceptor pipeline behavior | **KEEP** | NCAT | No redirect | Concrete persistence implementation and extension seam. |
| `docs/articles/audit-accountability-integration.md` | NCAT mutation audit records and external accountability integration seam | **KEEP** | NCAT | No redirect | Documents NCAT-owned contracts, identifiers, persistence boundaries, and integration sequence. General governance theory may be linked from Learning without moving this contract. |
| `docs/articles/dbcontext-audit-state-isolation.md` | DbContext audit-state lifecycle/isolation behavior | **KEEP** | NCAT | No redirect | Highly implementation-specific persistence behavior. |
| `docs/articles/audit-completion-outbox.md` | Optional durable local audit-completion handoff | **KEEP** | NCAT | No redirect | Documents an NCAT feature, delivery states, registration, and operator behavior. |
| `docs/articles/audit-reconciliation.md` | Audit reconciliation, integrity health, findings, and recovery | **KEEP** | NCAT | No redirect | Concrete NCAT operational control and recovery contract. |
| `docs/articles/optional-application-domain-layers.md` | General growth strategy for adding Application/Domain layers | **MOVE/COPY-THEN-REDIRECT** | Learning for application/domain-layer architecture; NCAT for a short extension note | First establish/copy canonical Learning material; then preserve this URL as a concise note describing NCAT's default compact structure and linking outward | Strongest educational-content candidate. Do not remove the NCAT URL. |
| `docs/articles/public-surface-v1.md` | Stable generated configuration/routes/symbols/order/compatibility boundaries | **KEEP** | NCAT | No redirect | Explicit public compatibility truth belongs with the implementation. |
| `docs/articles/deployment.md` | NCAT deployment behavior and hosting guidance | **KEEP** | NCAT | No redirect | Concrete deployment/runtime expectations. |
| `docs/articles/docker.md` | Repository/template Docker development behavior | **KEEP** | NCAT | No redirect | Concrete Dockerfile/Compose workflow. |
| `docs/articles/production-deployment-checklist.md` | Operator checklist for generated NCAT applications | **KEEP** | NCAT | No redirect | Explicitly operator-focused and tied to NCAT runtime features. |
| `docs/articles/runtime-readiness.md` | Runtime readiness and operational verification | **KEEP** | NCAT | No redirect | Current operational truth. |
| `docs/articles/v1-migration-guide.md` | Historical v0.5.x to v1.0 migration guide | **ARCHIVE** | NCAT historical archive | Preserve current URL as a historical pointer; move/copy full guide to an archive location only after inbound-link review | Valuable release history, but no longer part of the main current-consumer path. |
| `docs/articles/build-quality.md` | Current SDK, package, analyzer, reproducibility, coverage, release-build policy | **KEEP** | NCAT | No redirect | Current repository build/release truth, not general architecture education. |
| `docs/articles/container-publish.md` | Tag-driven NCAT container release workflow | **KEEP** | NCAT | No redirect | Repository release evidence and publishing contract. |
| `docs/articles/github-workflow.md` | NCAT contribution, CI, branch, PR, release, and automation workflow | **KEEP** | NCAT | No redirect | Repository operating policy belongs with the repository. |
| `docs/articles/documentation-ownership.md` | Cross-repository documentation ownership contract | **KEEP** | NCAT, with Learning as educational peer | Keep as durable ownership rule after this temporary inventory is removed | This page preserves the final ownership policy required by issue #413. |
| `docs/adr/index.md` | ADR landing page | **KEEP** | NCAT | No redirect | Repository-local design history. |
| `docs/adr/template.md` | ADR authoring template | **KEEP** | NCAT | No redirect | Maintainer artifact for future NCAT decisions. |
| `docs/adr/0001-use-structured-serilog-logging.md` | Decision record for Serilog | **KEEP** | NCAT | No redirect | Local rationale remains authoritative even if Learning teaches structured logging generally. |
| `docs/adr/0002-use-centralized-application-middleware-pipeline.md` | Decision record for centralized pipeline | **KEEP** | NCAT | No redirect | Local rationale remains authoritative even if Learning teaches middleware generally. |
| `docs/adr/0003-record-release-surface-and-distribution-strategy.md` | Decision record for package/release/distribution surface | **KEEP** | NCAT | No redirect | Release/distribution decision history. |
| `docs/adr/0004-keep-composite-savechanges-interceptor.md` | Decision record for SaveChanges interceptor architecture | **KEEP** | NCAT | No redirect | Persistence implementation rationale. |

## Published Generated Surfaces

These are part of the current documentation experience but are not hand-authored Markdown articles.

| Surface | Source | Disposition | Notes |
| --- | --- | --- | --- |
| API Reference | DocFX metadata generated from repository projects | **KEEP** | Generated implementation reference; NCAT remains canonical. |
| Test Coverage | CI-produced coverage publication linked from top navigation | **KEEP** | Current quality evidence. Keep generated and version/current-state oriented rather than educational. |

## Duplicate-Authority Hotspots

The following pages should be treated as the first refactor/migration queue because they mix exact NCAT behavior with concepts that are reusable beyond NCAT:

1. `optional-application-domain-layers.md` — most of the page teaches a general layering/growth strategy. Establish the Learning version first, then reduce the NCAT page to the scaffold-specific extension boundary.
2. `data-access.md` — retain provider choice, configuration, migrations, auditing, persistence normalization, and NCAT extension seams; move reusable EF Core/data-access teaching to Learning.
3. `authentication.md` and `authorization.md` — retain exact schemes, provider options, endpoints, claims transformation, fallback/default policies, and named policies; keep general identity/policy education in Learning.
4. `middleware.md`, `logging.md`, `error-handling.md`, `security-headers.md`, `rate-limiting.md`, `telemetry.md`, and `forwarded-headers.md` — preserve the concrete NCAT contract while linking reusable architecture/security/operations theory to Learning.

A redirect must not be introduced merely because Learning is the preferred educational repository. If a suitable canonical Learning page does not yet exist, creating or selecting that destination is a prerequisite follow-up task.

## URL and Inbound-Link Continuity Rules

Before any future move, archive, redirect-stub replacement, or deletion:

1. Search NCAT source, README files, ADRs, issue/PR templates, release notes, and Learning for inbound links to the existing page.
2. Preserve the existing published NCAT URL whenever practical. Because the site is static DocFX output, prefer an in-page pointer/stub unless the hosting layer provides a verified HTTP redirect mechanism.
3. Create or verify the destination before changing the source page.
4. Update the NCAT TOC only after the replacement destination and continuity mechanism exist.
5. Preserve repository-local history. ADRs and release/migration evidence should be marked historical or archived, not silently discarded.
6. Do not allow Learning to become the authority for NCAT runtime behavior. Links to Learning provide education; NCAT remains the versioned implementation contract.

## Follow-up Work Derived from This Inventory

The migration can be split into independent follow-up changes:

1. **Application/domain layering:** establish the canonical Learning article, copy/move reusable content, then convert the NCAT page to an implementation-specific pointer.
2. **Core runtime refactors:** narrow middleware, logging, error handling, security headers, forwarded headers, rate limiting, authentication, authorization, data access, and telemetry to exact NCAT behavior and add canonical Learning links where suitable pages exist.
3. **Historical navigation:** archive the v1 migration guide outside the main current-consumer path while preserving the existing URL.
4. **Link audit:** verify inbound NCAT/Learning/release links before any URL or TOC change.
5. **Final cleanup:** after the migration is complete and `documentation-ownership.md` still preserves the ownership contract, remove this temporary inventory if it no longer provides maintenance value.

## Result

This inventory intentionally classifies without restructuring. It keeps implementation and operational truth in NCAT, selects Learning as the canonical home for general architecture education, identifies duplicate-authority hotspots, preserves historical material, and defines the continuity constraints required before later migration work.
