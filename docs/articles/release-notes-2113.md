# NetCoreApplicationTemplate 2.11.3 Release Notes

Release date: 2026-10-15

NetCoreApplicationTemplate 2.11.3 is a backward-compatible security servicing
release for the feature-complete 2.x line. Generated projects now pin a patched
`System.Security.Cryptography.Pkcs` so they restore and build without NuGet
audit failures. The release also routes support questions to the organization
Discussions and carries repository-only test, documentation, and citation
maintenance. The package ID, template identity, short name, supported template
options, public application surface, container image digests, and `net10.0`
target are unchanged.

## Security servicing

- Pinned the transitive `System.Security.Cryptography.Pkcs` dependency to the
  patched 10.0.13 release. `Microsoft.Data.SqlClient` 6.1.6 previously resolved
  it to 9.0.11, which falls in the affected range of the .NET denial-of-service
  advisories CVE-2026-70162
  ([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7)) and
  CVE-2026-84008
  ([GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)).
- The pin is a single `PackageVersion` entry in the packaged
  `Directory.Packages.props`. Central transitive pinning is already enabled, so
  generated applications resolve 10.0.13 without any other change, and their
  lock files record it as a `CentralTransitive` dependency.
- 10.0.13 matches the template's .NET 10 baseline. The 9.x servicing release
  also addresses the advisories but would keep the dependency on the 9.x line.

## Generated-project changes

- `PersistenceTimestamp.NormalizeUtc` uses a single conditional instead of a
  `switch` over `DateTimeKind`, removing an unreachable branch. `Local` values
  are still converted to UTC, and `Utc` and `Unspecified` values are still
  marked as UTC. Behavior is unchanged.
- The packaged `PACKAGE-README.md` points community questions to
  [AsiBackbone organization Discussions](https://github.com/orgs/AsiBackbone/discussions).

## Repository maintenance

- Added repository-only branch-coverage tests for the classes that were below
  80% branch coverage in the published report. The new test files are excluded
  from generated scaffolds.
- Routed support questions in the README, support policy, and issue-form contact
  links to the organization Discussions, and asked project-specific posts to
  name `AsiBackbone/NetCoreApplicationTemplate` and the affected template
  version.
- Added the top-level `doi` field for the Zenodo concept DOI to `CITATION.cff`.
- Corrected the support policy's published-release marker from 2.11.1 to
  2.11.2 while this release is being prepared.

## Documentation site

- Aligned the documentation homepage, navigation, and footer with the
  organization landing-page standard, normalized homepage headings to sentence
  case, and added `scripts/Validate-LandingPage.ps1` to link validation and
  documentation publishing.
- DocFX builds in CI and publishing now run with `--warningsAsErrors`, and
  `scripts/Validate-DocFxStrictBuild.ps1` keeps every workflow build strict.
- Resolved the four DocFX link warnings and limited API metadata to the
  production projects under `src/`, with `scripts/Validate-ApiReference.ps1`
  guarding the published namespace set.
- Every published page now has search and social-preview descriptions, and the
  metadata script no longer inserts a literal `\n` when line endings differ.
  Generated coverage reports are marked `noindex`.
- Added a GitHub Pages documentation badge to the README and navbar logo
  spacing to the documentation stylesheet.

None of the documentation-site changes affect generated applications.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release.

Applications generated from 2.11.2 or earlier treat NuGet audit warnings as
errors, so they fail to build with NU1902 or NU1903 while they resolve a
vulnerable `System.Security.Cryptography.Pkcs`. To remediate an existing
application without regenerating it:

1. Add the pin to the application's `Directory.Packages.props`:

   ```xml
   <PackageVersion Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />
   ```

2. Refresh the lock files with `dotnet restore --force-evaluate`, then commit
   the updated `packages.lock.json` files.
3. Rebuild and run the application's tests.

Applications that have already moved to a `Microsoft.Data.SqlClient` release
that resolves a patched `System.Security.Cryptography.Pkcs` need no pin.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay and lock-file checks, scaffold-manifest
validation, package metadata and content checks, and documentation validation.
Hosted CI and the non-publishing template-package workflow provide the remaining
cross-platform, matrix, security, and release-artifact evidence. Stable
publication occurs only from the protected `v2.11.3` tag after the preparation
commit is merged to `main`.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [Template packaging](template-packaging.md)
- [Dependency update policy](dependency-update-policy.md)
- [Production deployment checklist](production-deployment-checklist.md)
- [Container release publishing](container-publish.md)
