# Documentation Currency Review — 2026-11-18

Tracking issue: [NCAT #718](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/718).

## Baseline and scope

The latest published stable template reviewed for this change is
[2.11.10](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.10).
The repository can prepare a newer package on `main` without changing the
identity of the latest published release.

The `main` documentation tree inventory contains 86 files: 67 Markdown
sources, three YAML navigation files, and 16 JSON, JavaScript, CSS, template,
image, or other resource files. The inventory covers:

| Area | Review treatment |
| --- | --- |
| `docs/index.md`, `docs/articles/index.md`, `docs/toc.yml`, `docs/articles/toc.yml` | Current landing, navigation, and release discovery |
| `docs/articles/template-packaging.md` | Published install pins, package identity and generated options |
| `docs/articles/` runtime guides | Authentication, authorization, middleware, EF Core, audit/outbox, telemetry, deployment, and operational boundaries |
| `docs/articles/` release and repository guidance | Build quality, container publishing, workflow and security controls |
| `docs/articles/release-notes-*.md` | Version-specific release evidence; preserve release identities |
| `docs/articles/v1-migration-guide.md` and `docs/articles/asibackbone-6-alignment-review.md` | Explicit historical migration and integration records |
| `docs/adr/` | Dated architecture decisions, not statements that an older release is currently published |
| `docs/api/` and `docs/api-pages/` | Generated API entry points and namespace navigation; inspect source-generated output in DocFX validation |
| `docs/examples/`, `docs/public/`, `docs/templates/`, `docs/images/` | Configuration examples, site presentation, and binary resources rather than package-release claims |

The review checked version-related prose and searched current-facing guidance
for older release assertions. Technical claims were compared with
`Directory.Build.props`, the template package identity, the version-consistency
validator, the release-update workflow, and the current publishing documents.
This is not a claim of independent, line-by-line security certification of
every documented runtime behavior.

## Findings and decisions

1. **Homepage:** The previous “Latest GitHub release” link concealed the
   reviewed version. The homepage now names the published release and links
   directly to its release record and matching documentation release notes.
2. **Package install:** The published package table and `dotnet new install`
   command already used 2.11.10; preserve these as a reproducible published
   baseline rather than replacing them with a future prepared repository version.
3. **Automatic update:** After each release, the release-update PR must update
   the homepage and published package examples together with README and SUPPORT.
4. **Validation:** `Validate-VersionConsistency.ps1` now checks the published
   homepage row, release notes, packaging version, and pinned installation
   examples against the latest published README marker.
5. **Evergreen guidance:** Keep runtime and operational documentation
   version-neutral unless a version is essential to an implementation boundary.
6. **Historical material:** Preserve the v1 migration guide, historical
   AsiBackbone 6 integration review, release notes, ADRs, package identity
   introduction history, intentionally pinned configurations and schema/wire
   version identifiers.

## Organization documentation context

Equivalent sources were considered in the other organization repositories:
`AsiBackbone/AsiBackbone` (governance package), `Learning` (teaching snapshots
and pinned baselines), `ReferenceApplication` (integration-reviewed pins),
`.github` (compatibility and assurance records), and `asibackbone.github.io`
(organization landing). Those repositories own their own runtime, educational,
and compatibility claims. This patch intentionally changes only NCAT files.

## Validation and publication follow-up

Run before merging:

```powershell
./scripts/Validate-VersionConsistency.ps1
./scripts/Validate-LandingPage.ps1 -SelfTest
```

Also run repository CI, relevant template packaging/scaffold tests, DocFX build,
and documentation/link validation. Those jobs require the complete toolchain
and are **not** recorded as passed by this review.

After the documentation workflow publishes, verify live
`/NetCoreApplicationTemplate/` HTML and the Template Packaging page. Compare
their displayed release and install command with the actual published stable
GitHub Release. Do not treat search-index snippets as deployed truth.

Separate follow-up: validate additional runtime and security claims against
each owning implementation and its tests when those contracts change; do not
automatically replace legitimate historical references with newer numbers.
