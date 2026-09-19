![.NET Core Application Social Preview](https://raw.githubusercontent.com/AsiBackbone/NetCoreApplicationTemplate/main/docs/images/social-preview.png)

# .NET Core Application Template

[![CI](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/ci.yml/badge.svg)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/ci.yml)
[![Coverage Report](https://img.shields.io/badge/coverage%20gate-75%25-brightgreen)](https://asibackbone.github.io/NetCoreApplicationTemplate/coverage/index.html)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/13645/badge)](https://www.bestpractices.dev/projects/13645)
[![Documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/publish-docs.yml/badge.svg)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/publish-docs.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE.txt)
[![GitHub Release](https://img.shields.io/github/v/release/AsiBackbone/NetCoreApplicationTemplate?display_name=tag)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/latest)
[![NuGet](https://img.shields.io/nuget/v/NetCoreApplicationTemplate?label=NuGet)](https://www.nuget.org/packages/NetCoreApplicationTemplate)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetCoreApplicationTemplate?label=downloads)](https://www.nuget.org/packages/NetCoreApplicationTemplate)
[![Zenodo DOI](https://img.shields.io/badge/DOI-10.5281%2Fzenodo.20373042-blue)](https://doi.org/10.5281/zenodo.20373042)

A production-oriented ASP.NET Core application template for .NET 10. Routed
endpoints require an authenticated user by default, anonymous access is explicit
and regression-tested, and the generated scaffold ships with structured logging,
security headers, forwarded-header validation, rate limiting, centralized
Problem Details error handling, EF Core data access and auditing patterns, health
checks, telemetry, and CI validation.

Install it:

```bash
dotnet new install NetCoreApplicationTemplate
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Tagged releases publish a durable, hashed evidence bundle for the exact NuGet
package and OCI image — separate SPDX SBOMs, provenance, the signed image digest,
and tested verification commands. See
[Container Release Publishing](docs/articles/container-publish.md#durable-release-assets).
NuGet author signing is tracked separately by
[ADR-0005](docs/adr/0005-defer-nuget-package-signing.md).

## Current Release

<!-- BEGIN LATEST_RELEASE -->
Current release: __[Release 2.9.0](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.9.0)__

Tag: `v2.9.0`
<!-- END LATEST_RELEASE -->

## Maintenance Status

The `2.x` line is feature-complete. NCAT is actively maintained for security
fixes, dependency servicing, and documentation corrections. New template
options, configuration surfaces, and runtime capabilities are out of scope for
this line.

Generated projects are not modified by later releases. Applications scaffolded
from any `2.x` version continue to build and run independently of the template's
release cadence.

See [SUPPORT.md](.github/SUPPORT.md) for the complete support policy and version
lifecycle.

## What This Template Secures by Default

The default scaffold enables cookie authentication as the session handler. It does not include local user accounts, a credential form, a seeded user, or an enabled external provider. Cookie authentication stores an identity after a sign-in flow succeeds; it does not verify credentials or provide a login path by itself.

Authorization determines whether that identity may access an endpoint or operation. NCAT configures a fallback authorization policy requiring an authenticated user for routed endpoints without authorization metadata. Intentionally public routes use explicit anonymous metadata such as `[AllowAnonymous]` or `.AllowAnonymous()`.

With the default provider configuration, `/Account/Login` therefore explains that no sign-in provider is configured, and protected routes remain unavailable to anonymous users. Before testing protected application routes, enable and configure an external provider or add a host-owned identity flow. Use `--authProvider none` only when an intentionally unauthenticated scaffold is appropriate.

The template also includes named policies for authenticated-user, administrator-role, and manage-application-permission requirements. These policy-based authorization controls layer stronger requirements beyond the authenticated-user baseline.

The phrase **secure baseline** in this project refers to concrete controls—closed-by-default routed endpoints, explicit anonymous exceptions, startup validation, request protection, secure headers, rate limiting, and centralized error handling. Deployment-specific trust boundaries, provider registrations, credentials, network exposure, and business authorization remain the consuming application's responsibility.

### Authentication-disabled opt-out

`--authProvider none` is an explicit architectural opt-out. It disables application authentication, cookie authentication, and the authenticated fallback policy in generated configuration. Unannotated routed endpoints are public in that variant until the consuming application adds another authentication mechanism and authorization posture.

## Quick Start from Source

```powershell
git clone https://github.com/AsiBackbone/NetCoreApplicationTemplate.git
cd NetCoreApplicationTemplate
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet run --project src/ProjectTemplate.Web
```

Run with Docker Compose:

```powershell
docker compose up --build
```

The Docker-hosted application is available at `http://localhost:8080`.

Health endpoints:

```text
http://localhost:8080/health
http://localhost:8080/health/ready
http://localhost:8080/health/live
```

Health routes are explicitly anonymous at the application layer for infrastructure probes. Production deployments should restrict their reachability through ingress, firewall, reverse-proxy, load-balancer, or service-mesh policy.

## Install and Use the Template Package

Install the published package:

```powershell
dotnet new install NetCoreApplicationTemplate::2.10.0
```

For local package validation, install the packed package directly:

```powershell
dotnet new install ./artifacts/template-package/NetCoreApplicationTemplate.2.10.0.nupkg
```

Generate the default cookie-authenticated scaffold:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Generate the explicit authentication-disabled variant:

```powershell
dotnet new netcoreapp-template `
  --name ContosoNoAuthSqlServer `
  --authProvider none `
  --dbProvider sqlserver
```

Template options:

| Option | Default | Supported values | Behavior |
|:---|:---|:---|:---|
| `--authProvider` | `cookie` | `cookie`, `none` | Selects either the cookie session handler with authenticated fallback access, or the explicit authentication-disabled opt-out. The cookie option still requires a configured sign-in provider or host-owned identity flow. |
| `--dbProvider` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated EF Core data access mode. |
| `--skipRestore` | `false` | `true`, `false` | Skips the post-create restore action. |

SQL Server scaffolds omit the SQLite-specific migration history. Generate a fresh SQL Server migration before applying database updates; see [Data Access](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/data-access.html).

Build and test generated output:

```powershell
cd ContosoSecurityPortal
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Project Goals

- Production-oriented ASP.NET Core startup and middleware organization.
- Cookie authentication and authenticated-by-default routed endpoints in the default scaffold.
- Explicit anonymous endpoint exceptions with regression coverage.
- Named role and permission authorization policies.
- Structured application and request logging.
- Centralized exception, status-code, and Problem Details handling.
- Reverse-proxy, security-header, rate-limiting, health-check, and telemetry foundations.
- EF Core provider and auditing patterns.
- Automated build, test, coverage, template smoke-test, CodeQL, and documentation workflows.
- Package-based `dotnet new` scaffold support.

## Authentication and Authorization Terminology

- **Authentication** establishes identity.
- **Authorization** determines permitted access.
- The **default authorization policy** applies when authorization is requested without a named policy.
- The **fallback authorization policy** applies to routed endpoints with no authorization metadata.
- **Explicit anonymous access** intentionally exempts a route from authorization.
- **Policy-based authorization** applies role, permission, claim, or custom requirements.

`DefaultPolicy` and `FallbackPolicy` are distinct ASP.NET Core concepts and are not used interchangeably in NCAT documentation.

## Documentation

- [Published documentation](https://asibackbone.github.io/NetCoreApplicationTemplate/)
- [Getting Started](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/getting-started.html)
- [Authentication](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/authentication.html)
- [Production Authentication Hardening](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/authentication-hardening.html)
- [Authorization](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/authorization.html)
- [Runtime Readiness](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/runtime-readiness.html)
- [Production Deployment Checklist](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/production-deployment-checklist.html)
- [Health Checks](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/health-checks.html)
- [Template Packaging](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/template-packaging.html)
- [Data Access](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/data-access.html)

Build documentation locally:

```powershell
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
```

## Repository and Generated Content

The repository contains source projects, tests, Docker support, DocFX documentation, CI workflows, release and governance files, template configuration, and package metadata.

Generated projects include application source, tests, Docker support, configuration examples, license and asset notices, and a consumer-oriented README. Repository-maintainer workflows, ADRs, community and governance policies, contribution policy, security policy, and release-management files are excluded from generated output.

## Related Projects

NCAT is self-contained. It has no dependency on any other AsiBackbone project and
requires no external governance, audit, or policy product.

- **[ASI Backbone Learning](https://asibackbone.github.io/Learning/)** — the
  organization's educational site for ASP.NET Core architecture, terminology,
  labs, and secure-by-default guidance. NCAT links to Learning for broader
  teaching rather than duplicating it. Learning does not define NCAT runtime
  behavior; this repository and its published documentation remain authoritative
  for NCAT's options, defaults, and contracts. See the
  [documentation ownership contract](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/documentation-ownership.html).

- **[AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)** — an optional
  .NET library for application-level policy decisions, acknowledgments, scoped
  capability grants, and decision audit records around protected operations. It
  complements but does not replace ASP.NET Core authentication or endpoint
  authorization, and NCAT does not require it.

## Support and Community

- Use [GitHub Discussions](https://github.com/AsiBackbone/NetCoreApplicationTemplate/discussions) for setup and usage questions, design or extension guidance, and community feedback.
- Use [GitHub Issues](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues) for reproducible bugs, documentation gaps, and focused feature requests.
- Report suspected vulnerabilities through the private process in [SECURITY.md](SECURITY.md).
- Participate under the repository [Code of Conduct](.github/CODE_OF_CONDUCT.md) and [Community Standards](.github/COMMUNITY_STANDARDS.md).
- See [Governance](.github/GOVERNANCE.md) and [Maintainers](.github/MAINTAINERS.md) for decision authority and operational ownership.

See [SUPPORT.md](.github/SUPPORT.md) for the complete support policy and version lifecycle.

## Versioning and Citation

This project follows Semantic Versioning. Version metadata is managed centrally for assemblies, packages, and releases.

Suggested citation:

```text
Cavell, Christopher D. NetCoreApplicationTemplate. Version 2.10.0. Zenodo. MIT License. https://doi.org/10.5281/zenodo.20373042
```

## License

This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) and [ASSETS-LICENSES.md](ASSETS-LICENSES.md).
