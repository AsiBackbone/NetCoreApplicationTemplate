# NetCoreApplicationTemplate 2.11.4 Release Notes

Release date: 2026-10-15

NetCoreApplicationTemplate 2.11.4 is a backward-compatible security servicing
release for the feature-complete 2.x line. It refreshes the container images
pinned in generated projects so that applications run on the .NET and ASP.NET
Core 10.0.13 runtimes. The package ID, template identity, short name, supported
template options, public application surface, and `net10.0` target are
unchanged.

## Why this release follows 2.11.3 on the same day

2.11.3 pinned `System.Security.Cryptography.Pkcs` 10.0.13 for NuGet audit, but
its container images still carried the 10.0.12 runtimes. During the 2.11.3
stable release, the container vulnerability gate found fixed high and critical
runtime vulnerabilities in the built image and stopped before signing or
publishing it. The 2.11.3 template package reached NuGet; its container image
and container release evidence did not. 2.11.4 supersedes 2.11.3.

## Container image servicing

- Refreshed the pinned ASP.NET Core 10.0 runtime image to the digest that
  carries the 10.0.13 runtimes:

  ```dockerfile
  FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS final
  ```

- Moved the build stage from the .NET SDK 10.0.401 image to the .NET SDK
  10.0.402 image:

  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
  ```

- Retained the existing image families, application build behavior, container
  port, and runtime configuration.

The refreshed runtime image resolves these findings reported against the 10.0.12
runtimes:

| Component | Critical | High |
| --- | --- | --- |
| ASP.NET Core runtime | CVE-2026-70188, CVE-2026-88092 | CVE-2026-70175, CVE-2026-70181, CVE-2026-70183, CVE-2026-70186, CVE-2026-96236 |
| .NET runtime | — | CVE-2026-58280, CVE-2026-69651, CVE-2026-96225, CVE-2026-96231, CVE-2026-96233 |

## Upgrade actions

Installing this template version does not modify applications generated from an
earlier release.

Applications generated from 2.11.3 or earlier that build container images should
update the two `FROM` lines in their `Dockerfile` to the digests above, rebuild
the image, and redeploy. Applications that run on a separately managed .NET 10
runtime should update that runtime to 10.0.13 or later.

Applications generated from 2.11.2 or earlier also need the
`System.Security.Cryptography.Pkcs` pin described in the
[2.11.3 release notes](release-notes-2113.md#upgrade-actions).

## Release verification

The release candidate is validated with locked restore, Release builds and
tests, formatting, template overlay and lock-file checks, scaffold-manifest
validation, package metadata and content checks, and documentation validation.
Hosted CI, the template smoke tests, and the non-publishing template-package
workflow provide the remaining cross-platform, matrix, security, and
release-artifact evidence, and the container vulnerability gate runs again
during stable publication. Stable publication occurs only from the protected
`v2.11.4` tag after the preparation commit is merged to `main` and CI has
completed on that commit.

See also:

- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)
- [2.11.3 release notes](release-notes-2113.md)
- [Docker development](docker.md)
- [Container release publishing](container-publish.md)
- [Dependency update policy](dependency-update-policy.md)
- [Production deployment checklist](production-deployment-checklist.md)
