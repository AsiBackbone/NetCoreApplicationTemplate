# NetCoreApplicationTemplate 2.11.8 Release Notes

Release date: 2026-11-06

NetCoreApplicationTemplate 2.11.8 is a backward-compatible maintenance release
for the feature-complete 2.x line. It refreshes pinned .NET 10 container images
and repository tooling, improves package discovery and project branding, adds a
five-minute evaluation path for the published template, and strengthens release
marker consistency. The package ID, template identity, short name, supported
template options, public application surface, and `net10.0` target are unchanged.

## Package discovery and visual identity

The NuGet package now includes reviewed `authentication`, `authorization`,
`ef-core`, `security-headers`, and `structured-logging` tags. Each tag describes
behavior present in the default scaffold; no template option or generated
capability was added. A repository lint test pins the reviewed tag set and the
NuGet length limit.

The repository, documentation site, package, favicon, and social-preview assets
now use the layered-pages glyph from the AsiBackbone glyph family. Asset license
records were updated with the replacement. These visual changes do not alter the
template identity or installation commands.

## Evaluation and project guidance

The repository README now includes a five-minute evaluation path that installs
the published package, generates a disposable application, and identifies the
observable authentication, security-header, and health-probe defaults. Source
build instructions are labeled for contributors and template maintainers.

Related-project guidance now identifies ReferenceApplication as the runnable
NCAT and AsiBackbone integration specimen and routes community feedback to the
organization discussion categories. Documentation navigation and search also
follow the shared organization contract.

## Supply-chain and tooling maintenance

- Refreshed the pinned .NET 10 SDK and ASP.NET Core runtime container image
  digests used by newly generated applications. Image tags and Dockerfile
  structure are unchanged.
- Updated ReportGenerator and pinned GitHub Actions used by validation,
  security scanning, documentation, container, and publication workflows.
- Restored reviewed, version-scoped dependency-check suppressions for Source
  Link and remaining OpenTelemetry .NET packages where scanner findings apply
  to unrelated ecosystems or components. Suppression validation continues to
  require an exact match with centrally pinned package versions.

## Release automation hardening

After a stable release is published, the publication workflow now advances the
README current-release block and both support-policy latest-release markers in
one pull request. Version-consistency validation rejects a newer published
marker than the prepared repository version and rejects drift among the three
published-release references.

## Compatibility and upgrade actions

Installing this template version does not modify applications generated from an
earlier release. Newly generated applications receive the refreshed container
image digests. Existing applications may adopt those digests independently
after their normal compatibility and security validation.

Package discovery tags, visual assets, documentation, repository tooling, and
release automation do not change generated application runtime behavior.

## Release verification

The release candidate is validated with version and tag consistency, locked
restore, Release builds and tests, formatting, template overlay, option-matrix
and lock-file checks, dependency suppression and release-evidence tests,
scaffold-manifest validation, package metadata and content checks, SBOM
generation, isolated package installation, generated-project builds and tests,
an EF Core migration generation and rebuild, and strict documentation
validation. Hosted CI, template smoke tests, and the non-publishing
template-package workflow provide the remaining cross-platform, option-matrix,
security, container, and release-artifact evidence. Stable publication occurs
only from the protected `v2.11.8` tag after the preparation commit is merged to
`main` and CI has completed on that commit.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [2.11.7 release notes](release-notes-2117.md)
- [Template packaging](template-packaging.md)
- [Build quality and reproducibility](build-quality.md)
- [Repository security profile](repository-security-profile.md)
