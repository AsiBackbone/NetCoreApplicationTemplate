# NetCoreApplicationTemplate 2.11.2 Release Notes

Release date: 2026-10-10

NetCoreApplicationTemplate 2.11.2 is a backward-compatible maintenance release
for the feature-complete 2.x line. It refreshes container image digests shipped
to generated projects and updates pinned GitHub Actions used by repository
automation. The package ID, template identity, short name, supported template
options, public application surface, and `net10.0` target are unchanged.

## Generated-project maintenance

- Refreshed the pinned .NET SDK 10.0.401 image digest used to build generated
  applications.
- Refreshed the pinned ASP.NET Core 10.0 runtime image digest used by generated
  application containers.
- Retained the existing .NET image families, application build behavior,
  container port, and runtime configuration.

## Repository automation maintenance

- Updated the pinned Harden-Runner action from 2.21.1 to 2.22.1.
- Updated the pinned CodeQL actions from 4.38.2 to 4.38.3.
- Updated the pinned upload-artifact action from 7.0.1 to 7.0.2 and the pinned
  download-artifact action from 8.0.1 to 8.0.2.
- Corrected the support policy's published-release marker from 2.11.0 to
  2.11.1 while this release is being prepared.

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release. No application migration is required.

Consumers that maintain generated applications independently may adopt the
container digest updates according to their own validation and deployment
schedule.

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay and lock-file checks, scaffold-manifest
validation, package metadata and content checks, and documentation validation.
Hosted CI and the non-publishing template-package workflow provide the remaining
cross-platform, matrix, security, and release-artifact evidence. Stable
publication occurs only from the protected `v2.11.2` tag after the preparation
commit is merged to `main`.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [Template packaging](template-packaging.md)
- [Docker development](docker.md)
- [Dependency update policy](dependency-update-policy.md)
- [Production deployment checklist](production-deployment-checklist.md)
- [Container release publishing](container-publish.md)
