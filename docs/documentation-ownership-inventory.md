# Documentation Ownership Inventory

> **Status:** Completed ownership review. The original planning inventory from [issue #413](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/413) was reconciled by [issue #487](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/487).<br>
> **Scope:** Durable disposition and completion record for the Markdown pages published by the NCAT DocFX site.<br>
> **Inventory date:** 2026-08-30.<br>
> **Completion review:** 2026-09-08.

## Purpose

This inventory assigns a long-term ownership disposition to every Markdown page currently published by the NCAT DocFX site.

The publication boundary is defined by `docs/docfx.json`:

- `docs/index.md`
- `docs/articles/**/*.md`
- `docs/adr/**/*.md`

Generated API reference and test-coverage surfaces are recorded separately because they are published outputs rather than hand-authored Markdown articles.

NCAT remains authoritative for concrete generated behavior, template options, configuration, deployment and operations, public compatibility surface, repository-local ADRs, release behavior, and runtime contracts. [ASI Backbone Learning](https://asibackbone.github.io/Learning/) is authoritative for reusable architecture education, conceptual explanation, tradeoff analysis, tutorials, and organization-level teaching.

## Disposition Definitions

- **KEEP** — authoritative NCAT implementation, runtime, operational, repository, reference, or explicitly historical documentation.

Earlier planning dispositions such as **REFACTOR**, **MOVE/COPY-THEN-REDIRECT**, and **ARCHIVE** no longer appear in the inventory. Their outcomes are recorded below as accepted **KEEP** pages with rationale. Keeping the implementation pages at their established URLs avoids circular canonical relationships: NCAT defines its behavior, while links to Learning provide reusable education.

## Inventory

| Document | Current role | Disposition | Canonical destination | NCAT replacement/redirect plan | Notes |
| --- | --- | --- | --- | --- | --- |
| `docs/index.md` | DocFX site landing page and ownership router | **KEEP** | NCAT | Keep current URL and implementation-first navigation | Correctly establishes NCAT vs Learning authority. |
| `docs/articles/index.md` | Template and operations documentation home | **KEEP** | NCAT | Keep as article landing page | Current-consumer navigation belongs with the implementation. |
| `docs/articles/template-packaging.md` | NuGet install, template generation, symbols, scaffold validation | **KEEP** | NCAT | No redirect | Exact package and generated-template behavior. |
| `docs/articles/getting-started.md` | Repository prerequisites, build, test, run, local docs | **KEEP** | NCAT | No redirect | Repository-specific onboarding. |
| `docs/articles/project-structure.md` | Generated solution/projects and responsibilities | **KEEP** | NCAT | No redirect | Describes the actual generated scaffold. |
| `docs/articles/configuration.md` | Application-owned configuration surface | **KEEP** | NCAT | No redirect | Configuration keys/defaults are runtime contract. |
| `docs/articles/middleware.md` | Concrete generated pipeline order, invariants, and extension points | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning link | Accepted as implementation-specific after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). ADR-0002 remains the NCAT rationale. |
| `docs/articles/authentication.md` | Authentication defaults, providers, endpoints, validation, and claims behavior | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as implementation-specific after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429); exact provider and endpoint behavior remains local. |
| `docs/articles/authentication-hardening.md` | Production hardening requirements for NCAT authentication | **KEEP** | NCAT | No redirect | Operator-facing, implementation-specific security guidance tied to NCAT options. |
| `docs/articles/authorization.md` | NCAT policies, fallback/default behavior, and endpoint classification | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as implementation-specific after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429); named policy behavior remains local. |
| `docs/articles/error-handling.md` | NCAT status pages, Problem Details, exception handling, and request correlation | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning link | Accepted as the exact NCAT response and production-safety contract after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/logging.md` | Serilog startup, runtime, and request-logging contract | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as implementation-specific after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). ADR-0001 remains the NCAT rationale. |
| `docs/articles/telemetry.md` | NCAT OpenTelemetry traces, metrics, and correlation | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as the generated instrumentation and export contract after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/security-headers.md` | NCAT security-header middleware and v1 contract | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as the exact header, option, exclusion, and intentional-opt-out contract after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/forwarded-headers.md` | Reverse-proxy configuration, trust defaults, and diagnostics | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as implementation and operator guidance after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/rate-limiting.md` | NCAT rate-limit defaults, partitioning, policies, and middleware order | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning links | Accepted as the concrete policy and production-configuration contract after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/health-checks.md` | NCAT health endpoints and health-check behavior | **KEEP** | NCAT | No redirect | Primarily an implementation and operations contract. |
| `docs/articles/api-versioning.md` | NCAT API-versioning package/configuration conventions | **KEEP** | NCAT | No redirect | Template-specific public/API behavior. |
| `docs/articles/data-access.md` | EF Core provider baseline, migrations, auditing, and persistence behavior | **KEEP** | NCAT behavior; Learning education | Refactor completed; preserve URL and Learning link | Accepted as the generated persistence and operations contract after [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429). |
| `docs/articles/ef-core-diagnostics.md` | NCAT EF Core diagnostic configuration | **KEEP** | NCAT | No redirect | Exact diagnostics behavior and configuration. |
| `docs/articles/ef-core-save-pipeline.md` | NCAT SaveChanges/interceptor pipeline behavior | **KEEP** | NCAT | No redirect | Concrete persistence implementation and extension seam. |
| `docs/articles/audit-accountability-integration.md` | NCAT mutation audit records and external accountability integration seam | **KEEP** | NCAT | No redirect | Documents NCAT-owned contracts, identifiers, persistence boundaries, and integration sequence. General governance theory may be linked from Learning without moving this contract. |
| `docs/articles/dbcontext-audit-state-isolation.md` | DbContext audit-state lifecycle/isolation behavior | **KEEP** | NCAT | No redirect | Highly implementation-specific persistence behavior. |
| `docs/articles/audit-completion-outbox.md` | Optional durable local audit-completion handoff | **KEEP** | NCAT | No redirect | Documents an NCAT feature, delivery states, registration, and operator behavior. |
| `docs/articles/audit-reconciliation.md` | Audit reconciliation, integrity health, findings, and recovery | **KEEP** | NCAT | No redirect | Concrete NCAT operational control and recovery contract. |
| `docs/articles/optional-application-domain-layers.md` | NCAT-specific application/domain extension boundaries | **KEEP** | NCAT behavior; Learning education | Refocus completed; preserve URL and canonical Learning link | Accepted after [PR #430](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/430). Learning owns general layering guidance; NCAT owns its scaffold and extension invariants. |
| `docs/articles/public-surface-v1.md` | Stable generated configuration/routes/symbols/order/compatibility boundaries | **KEEP** | NCAT | No redirect | Explicit public compatibility truth belongs with the implementation. |
| `docs/articles/deployment.md` | NCAT deployment behavior and hosting guidance | **KEEP** | NCAT | No redirect | Concrete deployment/runtime expectations. |
| `docs/articles/docker.md` | Repository/template Docker development behavior | **KEEP** | NCAT | No redirect | Concrete Dockerfile/Compose workflow. |
| `docs/articles/production-deployment-checklist.md` | Operator checklist for generated NCAT applications | **KEEP** | NCAT | No redirect | Explicitly operator-focused and tied to NCAT runtime features. |
| `docs/articles/runtime-readiness.md` | Runtime readiness and operational verification | **KEEP** | NCAT | No redirect | Current operational truth. |
| `docs/articles/v1-migration-guide.md` | Historical v0.5.x to v1.0 migration guide | **KEEP** | NCAT historical archive | Preserve the current URL; label the page historical and place it under Historical Archive navigation | Accepted as release history. The guide is outside current release/operations navigation while existing inbound URLs continue to resolve. |
| `docs/articles/build-quality.md` | Current SDK, package, analyzer, reproducibility, coverage, release-build policy | **KEEP** | NCAT | No redirect | Current repository build/release truth, not general architecture education. |
| `docs/articles/container-publish.md` | Tag-driven NCAT container release workflow | **KEEP** | NCAT | No redirect | Repository release evidence and publishing contract. |
| `docs/articles/repository-security-profile.md` | Current repository governance, branch-protection, and publishing controls | **KEEP** | NCAT | No redirect | Repository-specific security and release-governance requirements added by [issue #485](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/485). |
| `docs/articles/github-workflow.md` | NCAT contribution, CI, branch, PR, release, and automation workflow | **KEEP** | NCAT | No redirect | Repository operating policy belongs with the repository. |
| `docs/articles/documentation-ownership.md` | Cross-repository documentation ownership contract | **KEEP** | NCAT, with Learning as educational peer | Keep as the durable policy alongside this completed inventory | This page preserves the final ownership policy required by issue #413. |
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

## Resolved Duplicate-Authority Hotspots

The original inventory identified the following hotspots. Their accepted outcomes are:

1. `optional-application-domain-layers.md` now documents only NCAT's scaffold and extension boundaries and links to Learning's canonical layering article.
2. `data-access.md` retains generated provider choices, configuration, migrations, auditing, persistence normalization, and extension seams and links reusable EF Core teaching outward.
3. `authentication.md` and `authorization.md` retain exact schemes, provider options, endpoints, claims transformation, fallback/default policies, and named policies and link general identity/policy education outward.
4. `middleware.md`, `logging.md`, `error-handling.md`, `security-headers.md`, `rate-limiting.md`, `telemetry.md`, and `forwarded-headers.md` retain concrete NCAT contracts and link reusable architecture, security, and operations teaching outward.

These pages remain useful implementation references, so no redirects are needed. Learning links are deliberately one-way for education; Learning does not define the NCAT runtime contract.

## URL and Inbound-Link Continuity Rules

Before any future move, archive, redirect-stub replacement, or deletion:

1. Search NCAT source, README files, ADRs, issue/PR templates, release notes, and Learning for inbound links to the existing page.
2. Preserve the existing published NCAT URL whenever practical. Because the site is static DocFX output, prefer an in-page pointer/stub unless the hosting layer provides a verified HTTP redirect mechanism.
3. Create or verify the destination before changing the source page.
4. Update the NCAT TOC only after the replacement destination and continuity mechanism exist.
5. Preserve repository-local history. ADRs and release/migration evidence should be marked historical or archived, not silently discarded.
6. Do not allow Learning to become the authority for NCAT runtime behavior. Links to Learning provide education; NCAT remains the versioned implementation contract.

## Completion Record

The actions derived from the original inventory are complete:

1. **Application/domain layering:** [PR #430](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/430) refocused the retained NCAT page and linked the canonical Learning article without creating a circular behavior dependency.
2. **Core runtime refactors:** [PR #429](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/429) narrowed the implementation pages and added suitable Learning links.
3. **URL and link guardrails:** [PR #432](https://github.com/AsiBackbone/NetCoreApplicationTemplate/pull/432) preserved important publication URLs and added NCAT/Learning cross-repository validation.
4. **Historical navigation:** [issue #487](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/487) labeled the v1 guide as historical and removed it from current release and production paths without changing its published URL.
5. **Disposition review:** [issue #487](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/487) explicitly accepted every completed page as **KEEP** with the rationale recorded in this inventory.

## Result

The ownership migration is complete. NCAT retains implementation and operational truth, Learning is the canonical home for general architecture education, and historical material remains available at its established public URL. No actionable disposition remains in this inventory.
