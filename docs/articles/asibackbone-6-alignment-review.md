# AsiBackbone 6.0 Alignment Review

> **Status:** Complete on 2026-09-19 for [issue #524](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/524).

This review determines whether aligning NetCoreApplicationTemplate (NCAT) with AsiBackbone 6.0 and Learning 1.0 requires a breaking NCAT release. It does not make AsiBackbone a dependency of NCAT.

## Reviewed Baselines

| Repository | Reviewed release | Commit |
| --- | --- | --- |
| NetCoreApplicationTemplate | `main` after NCAT 2.10.0 | [`9b1cfd2`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/commit/9b1cfd2) |
| AsiBackbone | [`v6.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v6.0.0) | [`4dbd840`](https://github.com/AsiBackbone/AsiBackbone/commit/4dbd8407f7223dac635e704d4c6b138f818814fe) |
| Learning | [`v1.0.0`](https://github.com/AsiBackbone/Learning/releases/tag/v1.0.0) | [`6a0abb8`](https://github.com/AsiBackbone/Learning/commit/6a0abb8661fd81d64b2086447710c003d887d407) |

The final [AsiBackbone 5.x-to-6.0 migration guide](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/upgrade-500-to-600.md), [6.0 public API naming review](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/public-api-naming-600.md), [Learning compatibility guide](https://asibackbone.github.io/Learning/getting-started/learning-1-asibackbone-6-compatibility.html), and [Learning API boundary](https://asibackbone.github.io/Learning/getting-started/asibackbone-6-api-boundary.html) were used as the authoritative alignment inputs.

## Touchpoint Inventory

| Surface | Finding | Required action |
| --- | --- | --- |
| Package and build dependencies | No `AsiBackbone.*` package reference, namespace import, central package version, lock-file entry, or build dependency exists. | None. Keep NCAT self-contained. |
| Generated template source | No generated policy evaluator, endpoint marker, decision receipt, acknowledgment, capability grant, or AsiBackbone integration code exists. Generated AsiBackbone text is limited to NCAT repository/documentation links and the established template identity. | None. No generated behavior changes. |
| Template and public contracts | No AsiBackbone-specific template option, configuration key, environment variable, extension method, route, default, or project structure exists. | None. No consumer migration. |
| Tests and smoke tests | No test references an AsiBackbone package or API. Existing scaffold tests validate the organization-owned template identity and repository links, not product integration. | No new integration test is justified without an integration surface. |
| Examples and distribution | The Kubernetes example references NCAT's own `ghcr.io/asibackbone/netcoreapplicationtemplate` image. Package metadata and scaffold validation reference this repository only. | None; these are NCAT distribution identifiers. |
| Documentation | The README describes AsiBackbone as optional. Documentation ownership guidance routes reusable education to Learning, and 21 current files link to the production Learning site or repository. One NCAT article used “governance outbox” as generic prose. | Use “decision receipts” in the optional-product summary and ordinary “outbox” in generic prose. Retain NCAT-specific audit and outbox names. |

### File-Level Reference Inventory

- **NCAT organization and distribution identity:** `.template.config/template.json`, `eng/NetCoreApplicationTemplate.Template.csproj`, `eng/scaffold-manifest.default.json`, `eng/Validate-ScaffoldManifest.ps1`, `examples/kubernetes/projecttemplate-web.yaml`, and `src/ProjectTemplate.Web/Pages/Index.cshtml` contain NCAT's own AsiBackbone organization identity, repository, documentation, or container-image coordinates. They do not reference an AsiBackbone product API.
- **Optional AsiBackbone product description:** `README.md` is the only current NCAT file that describes the optional product. It explicitly says NCAT is self-contained and now uses the 6.0 “decision receipt” vocabulary.
- **Learning ownership and navigation:** `CONTRIBUTING.md`, `README.md`, `docs/index.md`, `docs/toc.yml`, `docs/documentation-ownership-inventory.md`, `docs/adr/index.md`, and `docs/articles/index.md` route readers to Learning or define the repository ownership boundary.
- **Learning educational deep links:** `docs/articles/authentication.md`, `authorization.md`, `configuration.md`, `data-access.md`, `documentation-ownership.md`, `error-handling.md`, `forwarded-headers.md`, `health-checks.md`, `logging.md`, `middleware.md`, `optional-application-domain-layers.md`, `rate-limiting.md`, `security-headers.md`, and `telemetry.md` link to production Learning guidance while retaining NCAT behavior locally.
- **Generic outbox wording:** `docs/articles/audit-accountability-integration.md` contained the only current “governance outbox” prose. It does not name or integrate an AsiBackbone `GovernanceOutbox*` API and now says “outbox.”
- **Historical records:** `CHANGELOG.md` contains organization-transfer, documentation-ownership, and prior release history. Those entries are historical facts, not current AsiBackbone API guidance, and require no rewriting.

## AsiBackbone 6.0 Decisions

- `AuditResidue` was replaced by `DecisionReceipt` for current CLR types and explanatory terminology. NCAT had no `AuditResidue` API or prose reference; the README's generic “decision audit records” wording is now “decision receipts.”
- Acknowledgment remains distinct from authorization and execution. NCAT contains no obsolete “acknowledgment handshake” wording or handshake API usage.
- Ordinary “outbox” is canonical in educational prose, while `GovernanceOutbox*` remains valid for exact AsiBackbone API names. NCAT does not use those APIs, so its one generic “governance outbox” phrase is now “outbox.”
- Removed `DefaultAsiBackbonePolicyEvaluator<TContext>` constructors do not affect NCAT; no evaluator is constructed or registered.
- Removed `RequireGovernancePolicy` overloads and the renamed endpoint marker do not affect NCAT; no endpoint uses them.
- No retired AsiBackbone compatibility alias, namespace, package surface, serialized contract, or persisted model is present in NCAT.

## Required Changes and Validation

Only documentation changes are required:

1. align the README's optional AsiBackbone description with the `DecisionReceipt` name;
2. use ordinary “outbox” terminology in generic NCAT guidance;
3. retain this review as the issue deliverable and link it from the documentation index.

No code, generated template, configuration, dependency, or test change is required. The normal NCAT build, test, scaffold, documentation, and link-validation gates remain sufficient because there is no optional/generated AsiBackbone integration to compile against 6.0.

Validation completed for this review:

- locked package restore passed;
- the Release build passed with zero warnings and zero errors;
- all 440 repository tests passed;
- formatting verification passed for all three tracked projects;
- a clean-checkout DocFX build included this page and completed with zero errors;
- the current-source terminology scan found none of the removed evaluator, endpoint-marker, audit-residue, acknowledgment-handshake, or generic “governance outbox” terms outside this historical comparison report.

## Cross-Repository Dependencies

The review depended on final AsiBackbone 6.0 naming and removal decisions and on publication of Learning 1.0. Both releases are now available, so no sequencing constraint remains. Learning remains the source for reusable education, AsiBackbone remains the source for package/API/runtime behavior, and NCAT remains the source for template defaults and generated contracts.

No follow-up implementation issue is needed. A future request to add explicit AsiBackbone integration would be a separate feature requiring its own template-contract, dependency, generated-output, and SemVer review.

## SemVer Recommendation

**Remain on the compatible NCAT 2.x line.**

The alignment changes only wording and review documentation. They do not rename or remove a template option, configuration key, extension method, generated type, route, artifact, or distribution identifier; change a default; add a required dependency; or require consumer migration. There is therefore no NCAT contract break that would justify 3.0.0.
