![.NET Core Application Social Preview](https://raw.githubusercontent.com/AsiBackbone/NetCoreApplicationTemplate/main/docs/images/social-preview.png)

# .NET Core Application Template

[![CI](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/ci.yml/badge.svg)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/ci.yml)
[![Coverage Report](https://img.shields.io/badge/coverage%20gate-75%25-brightgreen)](https://AsiBackbone.github.io/NetCoreApplicationTemplate/coverage/index.html)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/13645/badge)](https://www.bestpractices.dev/projects/13645)
[![Documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/publish-docs.yml/badge.svg)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/actions/workflows/publish-docs.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE.txt)
[![GitHub Release](https://img.shields.io/github/v/release/AsiBackbone/NetCoreApplicationTemplate?display_name=tag)](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/latest)
[![NuGet](https://img.shields.io/nuget/v/NetCoreApplicationTemplate?label=NuGet)](https://www.nuget.org/packages/NetCoreApplicationTemplate)
[![NuGet Downloads](https://img.shields.io/nuget/dt/NetCoreApplicationTemplate?label=downloads)](https://www.nuget.org/packages/NetCoreApplicationTemplate)
[![Zenodo DOI](https://img.shields.io/badge/DOI-10.5281%2Fzenodo.20373042-blue)](https://doi.org/10.5281/zenodo.20373042)

A reusable, production-oriented ASP.NET Core application template with structured logging, security headers, forwarded headers, rate limiting, centralized error handling, cookie authentication, authenticated-by-default routed endpoints, policy-based authorization, EF Core data access patterns, health checks, telemetry, and CI validation.

## Current Release

<!-- BEGIN LATEST_RELEASE -->
Current release: __[Release 2.6.2](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.6.2)__

Tag: `v2.6.2`
<!-- END LATEST_RELEASE -->

## Default Security Posture

The default scaffold enables local cookie authentication. Authentication establishes the caller's identity.

Authorization determines whether that identity may access an endpoint or operation. NCAT configures a fallback authorization policy requiring an authenticated user for routed endpoints without authorization metadata. Intentionally public routes use explicit anonymous metadata such as `[AllowAnonymous]` or `.AllowAnonymous()`.

The template also includes named policies for authenticated-user, administrator-role, and manage-application-permission requirements. These policy-based authorization controls layer stronger requirements beyond the authenticated-user baseline.

The phrase **secure baseline** in this project refers to concrete controls—closed-by-default routed endpoints, explicit anonymous exceptions, startup validation, request protection, secure headers, rate limiting, and centralized error handling. Deployment-specific trust boundaries, provider registrations, credentials, network exposure, and business authorization remain the consuming application's responsibility.

### Authentication-disabled opt-out

`--authProvider none` is an explicit architectural opt-out. It disables application authentication, cookie authentication, and the authenticated fallback policy in generated configuration. Unannotated routed endpoints are public in that variant until the consuming application adds another authentication mechanism and authorization posture.

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
dotnet new install NetCoreApplicationTemplate::2.6.2
```

For local package validation, install the packed package directly:

```powershell
dotnet new install ./artifacts/template-package/NetCoreApplicationTemplate.2.6.2.nupkg
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
| `--authProvider` | `cookie` | `cookie`, `none` | Selects either local cookie authentication with authenticated fallback access, or the explicit authentication-disabled opt-out. |
| `--dbProvider` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated EF Core data access mode. |
| `--skipRestore` | `false` | `true`, `false` | Skips the post-create restore action. |

Build and test generated output:

```powershell
cd ContosoSecurityPortal
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Authentication and Authorization Terminology

- **Authentication** establishes identity.
- **Authorization** determines permitted access.
- The **default authorization policy** applies when authorization is requested without a named policy.
- The **fallback authorization policy** applies to routed endpoints with no authorization metadata.
- **Explicit anonymous access** intentionally exempts a route from authorization.
- **Policy-based authorization** applies role, permission, claim, or custom requirements.

`DefaultPolicy` and `FallbackPolicy` are distinct ASP.NET Core concepts and are not used interchangeably in NCAT documentation.

## NCAT and AsiBackbone Boundary

NCAT supplies ASP.NET Core authentication, endpoint authorization, middleware ordering, request protection, observability, and application infrastructure.

A consuming application may integrate AsiBackbone for application-level policy decisions, acknowledgments, scoped capability grants, and decision audit records around protected operations. AsiBackbone complements but does not replace ASP.NET Core authentication or endpoint authorization.

## Documentation

- [Published documentation](https://AsiBackbone.github.io/NetCoreApplicationTemplate/)
- [Getting Started](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/getting-started.html)
- [Authentication](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/authentication.html)
- [Production Authentication Hardening](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/authentication-hardening.html)
- [Authorization](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/authorization.html)
- [Runtime Readiness](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/runtime-readiness.html)
- [Production Deployment Checklist](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/production-deployment-checklist.html)
- [Health Checks](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/health-checks.html)
- [Template Packaging](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/template-packaging.html)
- [Data Access](https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/data-access.html)

Build documentation locally:

```powershell
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
```

## Repository and Generated Content

The repository contains source projects, tests, Docker support, DocFX documentation, CI workflows, release and governance files, template configuration, and package metadata.

Generated projects include application source, tests, Docker support, configuration examples, license and asset notices, and a consumer-oriented README. Repository-maintainer workflows, ADRs, contribution policy, security policy, and release-management files are excluded from generated output.

## Versioning and Citation

This project follows Semantic Versioning. Version metadata is managed centrally for assemblies, packages, and releases.

Suggested citation:

```text
Cavell, Christopher D. NetCoreApplicationTemplate. Version 2.6.2. Zenodo. MIT License. https://doi.org/10.5281/zenodo.20373042
```

## License

This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) and [ASSETS-LICENSES.md](ASSETS-LICENSES.md).
