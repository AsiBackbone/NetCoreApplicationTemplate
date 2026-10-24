# NetCoreApplicationTemplate 2.11.6 Release Notes

Release date: 2026-10-24

NetCoreApplicationTemplate 2.11.6 is a backward-compatible maintenance release
for the feature-complete 2.x line. It corrects three generated-application
behaviors, publishes and validates the complete template option matrix, adds a
read-only repository-control drift audit, refreshes pinned container images and
GitHub Actions, services FsCheck and OpenTelemetry dependencies, and aligns
documentation with the live repository governance model. The package ID,
template identity, short name, supported template options, public application
surface, and `net10.0` target are unchanged.

## Generated-application corrections

- `/health/audit-integrity` no longer reports `Healthy` when no audit-tagged
  check is registered. An unconfigured application now returns HTTP 503 with
  `NotConfigured`. Once an audit check is registered, the endpoint retains its
  normal Healthy, Degraded, and Unhealthy behavior.
- EF Core design-time commands no longer produce a spurious fatal
  `HostAbortedException` log while intentionally stopping the application host.
  Genuine startup exceptions are still logged as fatal and rethrown.
- Development file logging now uses the documented
  `Logs/application-web-.log` path, matching the base generated settings and
  published configuration example.

## Template option assurance

The six existing `authProvider` and `dbProvider` combinations are now declared
in a machine-readable support matrix. CI generates its option jobs from that
contract and validates the matrix against the template choices and published
documentation. Every supported combination continues to be generated,
restored, built, and tested; this release adds no template option or new
generated capability.

## Repository and release controls

- Added a versioned desired-state manifest for the repository's hosted security
  and governance controls plus a read-only drift audit. The audit reports
  inaccessible administrator-only settings as unavailable rather than
  compliant and never changes repository settings.
- Added weekly and on-demand audit automation and self-tests that enforce the
  audit's read-only behavior.
- Updated repository, security, release, and container documentation to match
  the live main-branch ruleset and approval flow, including the absence of
  bypass actors.
- Standardized first-contact descriptions around NCAT's role as a reusable,
  production-oriented baseline and retained the explicit boundary that a
  generated application still requires application-specific security work.

These repository-control changes do not alter generated applications or the
template package surface.

## Dependency, container, and workflow servicing

- Updated FsCheck from 3.4.0 to 3.6.0 and all four OpenTelemetry packages from
  1.20.0 to 1.20.1, then refreshed the Web and test-project lock files.
- Refreshed the pinned .NET SDK 10.0.402 and ASP.NET Core 10.0 image digests in
  the generated Dockerfile. Image tags, target framework, and Dockerfile stages
  remain unchanged.
- Updated pinned GitHub Actions used by repository and release automation,
  including CodeQL 4.39.1, Harden-Runner 2.22.2, Docker Login Action 4.7.0,
  and Zizmor Action 0.8.0.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release.

Existing applications can adopt the audit-integrity correction by returning a
non-success response when no audit-tagged check is registered, apply the
`HostAbortedException` filter to the bootstrap exception handler, and align the
Development log path independently. Applications using the generated
Dockerfile can also refresh its two pinned image digests after their normal
container validation.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay, option-matrix and lock-file checks,
dependency suppression and release-evidence tests, scaffold-manifest
validation, package metadata and content checks, SBOM generation, isolated
package installation, generated-project builds and tests, an EF Core migration
generation and rebuild, and strict documentation validation. Hosted CI,
template smoke tests, and the non-publishing template-package workflow provide
the remaining cross-platform, option-matrix, security, and release-artifact
evidence. Stable publication occurs only from the protected `v2.11.6` tag after
the preparation commit is merged to `main` and CI has completed on that commit.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [2.11.5 release notes](release-notes-2115.md)
- [Template option support matrix](template-option-matrix.md)
- [Template packaging](template-packaging.md)
- [Build quality and reproducibility](build-quality.md)
- [Repository security profile](repository-security-profile.md)
- [Production deployment checklist](production-deployment-checklist.md)
