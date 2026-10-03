# NetCoreApplicationTemplate 2.11.1 Release Notes

Release date: 2026-10-03

NetCoreApplicationTemplate 2.11.1 is a backward-compatible maintenance release
for the feature-complete 2.x line. It refreshes generated-project dependencies,
hardens local container defaults, and improves repository automation,
validation, test coverage, and documentation. The package ID, template
identity, short name, supported template options, public application surface,
and `net10.0` target are unchanged.

## Generated-project maintenance

- Updated the generated application and test dependency baseline, including
  OpenTelemetry hosting and OTLP exporter 1.19.1, OpenTelemetry HTTP
  instrumentation 1.19.0, System.Drawing.Common 10.0.12,
  SQLitePCLRaw.bundle_e_sqlite3 3.0.5, Microsoft.SourceLink.GitHub 10.0.401,
  FsCheck 3.4.0, and coverlet.MTP 10.1.0.
- Refreshed the pinned .NET SDK and ASP.NET Core container image digests while
  retaining the existing .NET 10 image families and container port.
- Corrected generated asset notices so their package versions match the locked
  dependency graph.
- Changed Docker Compose local development to publish port 8080 on
  `127.0.0.1` instead of every network interface. This prevents another
  machine on the network from reaching the development container and spoofing
  forwarded client addresses under the permissive local proxy configuration.

## Repository reliability and documentation

- Pinned workflow jobs to Ubuntu 24.04 ahead of the `ubuntu-latest` migration,
  while retaining the cross-platform template smoke-test matrix.
- Restored project status automation and made its limited project-token
  behavior explicit when an issue is not present in the configured project.
- Expanded link validation across security, package, governance, and community
  documents and corrected the broken relative links it exposed.
- Added an automatically generated XML sitemap to the DocFX site and removed
  invalid inherited XML-documentation references.
- Added focused tests for audit hosted services, reconciliation, transactions,
  completion-outbox behavior, and account-controller paths.
- Added the repository-local NCAT code-review agent skill and refreshed
  version-specific audit-contract and coverage guidance.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release. No application migration is required.

Newly generated projects bind the Docker Compose development port to loopback.
To reach that container from another machine, deliberately change the host
binding and replace the permissive development forwarded-header trust with
explicit known proxies or networks.

Consumers that maintain generated applications independently may review and
adopt the dependency updates according to their own validation and deployment
schedule.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay and lock-file checks, scaffold-manifest
validation, package metadata and content checks, and documentation validation.
Hosted CI and the non-publishing template-package workflow provide the remaining
cross-platform, matrix, security, and release-artifact evidence. Stable
publication occurs only from the protected `v2.11.1` tag after the preparation
commit is merged to `main`.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [Template packaging](template-packaging.md)
- [Docker development](docker.md)
- [Dependency update policy](dependency-update-policy.md)
- [Production deployment checklist](production-deployment-checklist.md)
- [Container release publishing](container-publish.md)
