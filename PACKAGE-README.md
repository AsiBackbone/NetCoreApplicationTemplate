![.NET Core Application Social Preview](https://raw.githubusercontent.com/AsiBackbone/NetCoreApplicationTemplate/main/docs/images/social-preview.png)

# .NET Core Application Template

A reusable `dotnet new` template for creating a production-oriented ASP.NET Core application baseline with structured logging, security headers, forwarded headers, rate limiting, centralized error handling, cookie authentication, authenticated-by-default routed endpoints, policy-based authorization, and EF Core-ready structure.

This README is intended for NuGet package consumers. The full repository README and documentation site provide deeper implementation and maintainer guidance.

## Template identity

| Item | Value |
|---|---|
| Package ID | `NetCoreApplicationTemplate` |
| Template short name | `netcoreapp-template` |
| Default authentication provider | `cookie` |
| Default data provider | `sqlite` |

## Install

Install the template package from NuGet:

```text
dotnet new install NetCoreApplicationTemplate::2.6.0
```

For local package validation, install a packed package directly:

```text
dotnet new install ./artifacts/template-package/NetCoreApplicationTemplate.2.6.0.nupkg
```

## Generate a project

Create a default scaffold:

```text
dotnet new netcoreapp-template -n ContosoSecurityPortal
```

The default scaffold enables local cookie authentication and a fallback authorization policy that requires an authenticated user for routed endpoints without authorization metadata. Intentionally public routes must use explicit anonymous metadata such as `[AllowAnonymous]` or `.AllowAnonymous()`.

Generate with application authentication disabled:

```text
dotnet new netcoreapp-template --name ContosoNoAuth --authProvider none
```

`--authProvider none` is an explicit opt-out. It disables application authentication, cookie authentication, and the authenticated fallback policy in generated configuration. Unannotated routed endpoints are therefore public until the consuming application adds another authentication mechanism and authorization posture.

Generate with SQL Server selected:

```text
dotnet new netcoreapp-template --name ContosoSqlServer --dbProvider sqlserver
```

Generate with authentication disabled and SQL Server selected:

```text
dotnet new netcoreapp-template --name ContosoNoAuthSqlServer --authProvider none --dbProvider sqlserver
```

## Authentication and authorization terminology

- **Authentication** establishes the caller's identity.
- **Authorization** determines whether that identity may access an endpoint or operation.
- The **fallback authorization policy** applies to routed endpoints that contain no authorization metadata.
- **Policy-based authorization** adds role, permission, claim, or custom requirements beyond the authenticated-user baseline.

## Template options

| Option | Default | Supported values | Description |
|---|---|---|---|
| `--authProvider` | `cookie` | `cookie`, `none` | Selects either the default cookie-authentication and authenticated-fallback posture or the explicit authentication-disabled opt-out. |
| `--dbProvider` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated EF Core provider configuration. |
| `--skipRestore` | `false` | `true`, `false` | Skips the post-create NuGet restore action when set to `true`. |

## Build and test generated output

```text
cd ContosoSecurityPortal
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Update

Install the newer package version with the same package identity:

```text
dotnet new install NetCoreApplicationTemplate::2.6.0
```

## Uninstall

```text
dotnet new uninstall NetCoreApplicationTemplate
```

## Additional resources

- GitHub repository: https://github.com/AsiBackbone/NetCoreApplicationTemplate
- Published documentation: https://AsiBackbone.github.io/NetCoreApplicationTemplate/
- Template packaging documentation: https://AsiBackbone.github.io/NetCoreApplicationTemplate/articles/template-packaging.html
- Changelog: CHANGELOG.md
- License: LICENSE.txt
- Releases: https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases
- Coverage Report: https://AsiBackbone.github.io/NetCoreApplicationTemplate/coverage/index.html

Generated projects receive their own consumer-oriented README from the scaffold. This package README is intentionally limited to NuGet installation, scaffold options, validation commands, and consumer-facing reference links.
