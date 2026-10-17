# NetCoreApplicationTemplate 2.11.5 Release Notes

Release date: 2026-10-17

NetCoreApplicationTemplate 2.11.5 is a backward-compatible maintenance release
for the feature-complete 2.x line. It fixes warnings-as-errors failures caused
by analyzer diagnostics in generated EF Core migrations, services repository
test dependencies and GitHub Actions, and links the documentation to the
ReferenceApplication integration walkthrough. The package ID, template
identity, short name, supported template options, public application surface,
and `net10.0` target are unchanged.

## Generated EF Core migration compatibility

Files under `**/Data/Migrations/*.cs` are now marked as generated code in the
template's `.editorconfig`. The generated-file scope suppresses three analyzer
diagnostics that valid `dotnet ef migrations add` output can trigger:

- `IDE0005` for generated using directives.
- `IDE0161` for the namespace form emitted by EF Core tooling.
- `CA1861` for generated array arguments.

The suppressions apply only to generated migration files. Strict analyzer and
warnings-as-errors enforcement remains unchanged for application source code.

## Repository maintenance

- Updated xUnit.net to 4.0.2 and xunit.runner.visualstudio to 4.0.1 in the
  repository test project and refreshed its locked dependency graph. Generated
  application dependencies are unchanged.
- Updated pinned GitHub Actions used by repository automation, including
  CodeQL 4.39.0.
- Corrected the support policy's current-release marker to 2.11.4 while this
  release is being prepared.

## Documentation integration

The documentation ownership contract, global navigation, and Related projects
section now link to the ReferenceApplication. That downstream application owns
the ordered walkthrough showing how an NCAT-generated host evolves toward
AsiBackbone integration; NCAT remains authoritative for template behavior.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release.

Existing applications whose generated EF Core migrations fail strict builds on
`IDE0005`, `IDE0161`, or `CA1861` can copy the scoped
`[**/Data/Migrations/*.cs]` analyzer configuration from this release's
`.editorconfig`.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay and lock-file checks, dependency
suppression and release-evidence tests, scaffold-manifest validation, package
metadata and content checks, isolated package installation, generated-project
builds and tests, and strict documentation validation. Hosted CI, template
smoke tests, and the non-publishing template-package workflow provide the
remaining cross-platform, option-matrix, security, and release-artifact
evidence. Stable publication occurs only from the protected `v2.11.5` tag after
the preparation commit is merged to `main` and CI has completed on that commit.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [2.11.4 release notes](release-notes-2114.md)
- [Template packaging](template-packaging.md)
- [Build quality and reproducibility](build-quality.md)
- [Dependency update policy](dependency-update-policy.md)
- [Production deployment checklist](production-deployment-checklist.md)
