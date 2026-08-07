# v1.0 Migration Guide

This guide explains how to move from a `v0.5.x` preview version of the .NET Core Application Template to the first stable `v1.0.0` release.

The `v1.0.0` release establishes the first stable public surface for the template. Earlier `0.x` releases should be treated as preview releases that may include release-readiness, packaging, documentation, workflow, and scaffold-surface changes before the stable contract is finalized.

## Who Should Use This Guide

Use this guide if you:

- Created an application from a `v0.5.x` version of the template.
- Installed a preview template package locally.
- Cloned the repository and customized it directly.
- Need to compare an existing generated application against the stable `v1.0.0` public surface.
- Need to prepare release notes, deployment review, or internal upgrade documentation.

## Recommended Migration Strategy

For most consumers, the safest migration path is not an in-place overwrite.

Instead:

1. Generate a clean application from the `v1.0.0` template.
2. Compare the generated output against the existing application.
3. Move intentional application-specific changes forward.
4. Review configuration, middleware, deployment, and public-surface differences.
5. Validate the migrated application with restore, build, test, and runtime smoke checks.

This approach avoids accidentally losing local application changes while still allowing consumers to adopt the stable baseline.

## Before You Begin

Review these documents before migration:

- [Public Surface v1.0](public-surface-v1.md)
- [Template Packaging](template-packaging.md)
- [Configuration](configuration.md)
- [Deployment Notes](deployment.md)
- [Production Deployment Checklist](production-deployment-checklist.md)
- [Release Checklist](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md)
- [Changelog](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/CHANGELOG.md)

## Template Identity

The stable template identity is:

| Field | Value |
|:---|:---|
| NuGet package ID | `CDCavell.NetCoreApplicationTemplate` |
| Template identity | `CDCavell.NetCoreApplicationTemplate.CSharp` |
| Template group identity | `CDCavell.NetCoreApplicationTemplate` |
| Template name | `.NET Core Application Template` |
| `dotnet new` short name | `netcoreapp-template` |
| Source replacement name | `ProjectTemplate` |

After `v1.0.0`, changes to these values are treated as public-surface changes.

## Template Parameters

The template currently exposes the following consumer parameters:

| Parameter | Type | Default | Supported values | Description |
|:---|:---|:---|:---|:---|
| `skipRestore` | `bool` | `false` | `true`, `false` | Skips the post-create NuGet restore action when set to `true`. |
| `authProvider` | `choice` | `cookie` | `cookie`, `none` | Selects the generated authentication baseline. Use `cookie` for the default cookie-authentication-ready baseline or `none` to generate the application with application authentication disabled by default. |
| `dbProvider` | `choice` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated data access mode. Use `sqlite` for the default local development configuration, `sqlserver` for the SQL Server provider configuration, or `none` to generate the application with EF Core data access disabled. |

Default scaffold:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

Skip automatic restore:
```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal --skipRestore true
```

Generate with authentication disabled:
```powershell
dotnet new netcoreapp-template `
  -n ContosoNoAuthPortal `
  --authProvider none
```

Generate with SQL Server provider configuration:
```powershell
dotnet new netcoreapp-template `
  -n ContosoSqlServerPortal `
  --dbProvider sqlserver
```

Generate with EF Core data access disabled:

```powershell
dotnet new netcoreapp-template `
  -n ContosoNoDataAccessPortal `
  --dbProvider none
```

Generate a non-default scaffold with authentication disabled and SQL Server configuration:
```powershell
dotnet new netcoreapp-template `
  -n ContosoNoAuthSqlServer `
  --authProvider none `
  --dbProvider sqlserver
```


When `skipRestore` is not supplied, the template attempts to run `dotnet restore` after project creation.


## Recommended Clean Migration Workflow

Create a clean comparison workspace:

```powershell
mkdir C:\Temp\NetCoreApplicationTemplate-v1-migration
cd C:\Temp\NetCoreApplicationTemplate-v1-migration
```

Uninstall any older local template package if needed:

```powershell
dotnet new uninstall CDCavell.NetCoreApplicationTemplate
```

Install the stable package:

```powershell
dotnet new install CDCavell.NetCoreApplicationTemplate
```

Generate a clean `v1.0.0` application:

```powershell
dotnet new netcoreapp-template -n ContosoSecurityPortal-v1
```

Compare the existing application to the clean v1.0 scaffold:

```powershell
git diff --no-index C:\Path\To\ExistingApp C:\Temp\NetCoreApplicationTemplate-v1-migration\ContosoSecurityPortal-v1
```

Treat differences as review items rather than automatic replacements.

Then restore, build, and test the migrated application:
```powershell
cd C:\Path\To\MigratedApp
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Areas to Compare

Review these areas carefully:

```
[ ] Solution and project structure.
[ ] Project file package references and SDK settings.
[ ] Directory.Build.props and Directory.Packages.props.
[ ] Program.cs startup composition.
[ ] Middleware ordering.
[ ] Configuration sections and option names.
[ ] appsettings.json defaults.
[ ] appsettings.Development.json local overrides.
[ ] Dockerfile and docker-compose.yml.
[ ] Health check paths.
[ ] Error handling routes.
[ ] Security header defaults.
[ ] Rate limiting defaults.
[ ] Logging and telemetry configuration.
[ ] Authentication and authorization configuration.
[ ] EF Core provider, disabled data access mode, and migration strategy reviewed.
[ ] Tests and smoke-test expectations.
[ ] README and generated consumer instructions.
```

## Behavior and Default Changes to Review

The v1.0.0 baseline should be reviewed as an intentional production-oriented template surface, not just a set of files.

Pay special attention to:
- Middleware order.
- Forwarded header trust boundaries.
- Structured request logging.
- Centralized exception handling.
- Problem Details behavior.
- Security headers and Content Security Policy.
- HTTPS redirection behavior.
- Static file behavior.
- Routing, CORS, rate limiting, authentication, and authorization order.
- Health check endpoints.
- Data access provider selection or disabled data access mode.
- Explicit migration execution rather than automatic production startup migration.

## Breaking Change Review

Because `v0.5.x` releases are preview releases, consumers should assume the stable `v1.0.0` baseline may include changes that require manual review.

After `v1.0.0`, a change is breaking when it causes an application generated from the previous stable version to require code, configuration, deployment, or usage changes that were not clearly optional.

Use [Public Surface `v1.0.0`](public-surface-v1.md) as the compatibility reference.

## Configuration Review

Confirm the migrated application still has the expected configuration sections:

```
[ ] ConnectionStrings
[ ] AllowedHosts
[ ] Serilog
[ ] ProjectTemplate:ForwardedHeaders
[ ] ProjectTemplate:SecurityHeaders
[ ] ProjectTemplate:RateLimiting
[ ] ProjectTemplate:RequestLogging
[ ] ProjectTemplate:OpenTelemetry
[ ] ProjectTemplate:Authentication
[ ] ProjectTemplate:ClaimsTransformation
[ ] ProjectTemplate:Authorization
[ ] ProjectTemplate:DataAccess
[ ] ProjectTemplate:ApiVersioning
```

Production values should come from environment variables, platform configuration, or an approved secret store. Do not copy production secrets into committed JSON files.

## Deployment Review

Before deploying a migrated application, complete the Production Deployment Checklist.

At minimum, confirm:

```
[ ] ASPNETCORE_ENVIRONMENT is set correctly.
[ ] AllowedHosts matches public host names.
[ ] Forwarded headers match the proxy/load balancer topology.
[ ] Production secrets are not committed.
[ ] Data access provider or disabled data access mode reviewed.
[ ] Database provider and connection string names are correct, when data access is enabled.
[ ] Data access provider and migration strategy reviewed, when applicable.
[ ] Migrations are applied intentionally, when data access is enabled.
[ ] Health checks match infrastructure probe paths.
[ ] Rate limits are appropriate for expected traffic.
[ ] Logs do not expose sensitive values.
[ ] Error responses do not expose implementation details.
```

## Rollback Guidance

Do not overwrite a working production application with newly scaffolded output.

Recommended rollback posture:

- Keep the existing application branch intact.
- Migrate on a separate branch.
- Commit comparison changes in small reviewable batches.
- Keep the previous deployable version available until the migrated version passes smoke testing.
- If package artifacts are already published, prefer a corrective patch release over rewriting history.

For release-level rollback and hotfix policy, see [Release Checklist](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md).

## Migration Acceptance Checklist

Use this checklist before treating a `v0.5.x` application as migrated to the `v1.0.0` baseline:

```
[ ] Clean v1.0 scaffold generated successfully.
[ ] Existing application compared against clean v1.0 scaffold.
[ ] Intentional local customizations identified.
[ ] Middleware order reviewed.
[ ] Configuration keys reviewed.
[ ] Template parameter usage reviewed.
[ ] Deployment settings reviewed.
[ ] Health check behavior verified.
[ ] Logging and telemetry reviewed.
[ ] Security headers reviewed.
[ ] Rate limits reviewed.
[ ] Authentication and authorization settings reviewed.
[ ] Data access provider or disabled data access mode reviewed.
[ ] Docker/container behavior reviewed, if applicable.
[ ] Restore succeeds.
[ ] Build succeeds in Release configuration.
[ ] Tests pass.
[ ] Application starts locally.
[ ] README and generated consumer documentation reviewed.
[ ] Production deployment checklist completed.
```

## Related Documents

[Public Surface v1.0](public-surface-v1.md)
[Template Packaging](template-packaging.md)
[Production Deployment Checklist](production-deployment-checklist.md)
[Deployment Notes](deployment.md)
[Configuration](configuration.md)
[Middleware Pipeline](middleware.md)
[Logging](logging.md)
[Telemetry](telemetry.md)
[Error Handling](error-handling.md)
[Security Headers](security-headers.md)
[Rate Limiting](rate-limiting.md)
[Health Checks](health-checks.md)
[Authentication](authentication.md)
[Authorization](authorization.md)
[Data Access](data-access.md)
