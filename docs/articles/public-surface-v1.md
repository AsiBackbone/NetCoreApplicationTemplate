# Public Surface

This article defines the public compatibility surface for the current stable .NET Core Application Template release line.

After `v1.0.0`, this document should be used with [ADR-0003](../adr/0003-record-release-surface-and-distribution-strategy.md), [Release Checklist](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md), and the [v1.0 Migration Guide](v1-migration-guide.md) to decide whether a future change is SemVer-major, SemVer-minor, SemVer-patch, or internal-only.

## Purpose

The public surface is the set of template behaviors, generated artifacts, configuration keys, routes, conventions, and distribution identifiers that consumers may reasonably depend on after a stable release.

A change is breaking when it causes an application generated from the previous stable version to require code, configuration, deployment, or usage changes that were not clearly optional.

## Package and Template Identity

The following identifiers are part of the stable public surface.

| Surface | Committed value |
|:---|:---|
| Current NuGet package ID | `NetCoreApplicationTemplate` |
| Previous NuGet package ID | `CDCavell.NetCoreApplicationTemplate` |
| Template identity | `CDCavell.NetCoreApplicationTemplate.CSharp` |
| Template group identity | `CDCavell.NetCoreApplicationTemplate` |
| Template name | `.NET Core Application Template` |
| `dotnet new` short name | `netcoreapp-template` |
| Source replacement name | `ProjectTemplate` |
| Preferred name directory | `true` |

The `2.0.0` release moved the public NuGet package ID to `NetCoreApplicationTemplate`. The internal template identity and group identity remain unchanged for template metadata continuity.

Changing, removing, or repurposing these values after a stable release is a breaking change unless the old value remains supported through an intentional compatibility path.

## Template Symbols and Defaults

The following template symbols are part of the public surface.

| Symbol | Type | Default | Supported values | Contract |
|:---|:---|:---|:---|:---|
| `skipRestore` | `bool` | `false` | `true`, `false` | Controls whether the post-create NuGet restore action runs. |
| `authProvider` | `choice` | `cookie` | `cookie`, `none` | Selects the generated authentication baseline. `cookie` generates the default cookie-authentication-ready baseline. `none` generates the application with application authentication disabled by default. |
| `dbProvider` | `choice` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated data access mode. `sqlite` includes the default SQLite migration history. `sqlserver` configures SQL Server and omits SQLite-specific migrations and migration tests. `none` generates the application with EF Core data access disabled. |

Breaking changes include renaming a symbol, removing it, removing a documented supported value, inverting its meaning, changing its default in a behavior-changing way, or making generated output depend on it differently without a migration path.

Adding a new optional symbol with a safe default is normally a minor change.

## Generated Output Structure

The following generated structure is part of the public surface.

```text
/
├── ProjectTemplate.slnx
├── src/
│   ├── ProjectTemplate.Web/
│   └── ProjectTemplate.Infrastructure/
├── tests/
│   └── ProjectTemplate.Web.Tests/
├── Dockerfile
├── docker-compose.yml
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── .env.example
├── .dockerignore
├── .gitattributes
├── .gitignore
├── ASSETS-LICENSES.md
└── LICENSE.txt
```

The generated project responsibilities are:

| Project | Contract |
|:---|:---|
| `ProjectTemplate.Web` | ASP.NET Core host, startup composition, middleware pipeline, authentication and authorization registration, endpoint mapping, Razor Pages, MVC controllers, health checks, request logging, error handling, and web-facing configuration. |
| `ProjectTemplate.Infrastructure` | Infrastructure and persistence concerns used by the web application, including EF Core data access and related implementation details. |
| `ProjectTemplate.Web.Tests` | Tests for startup, configuration, middleware, authentication, authorization, and runtime behavior. |

Breaking changes include removing, renaming, or relocating these projects, changing the generated solution entry point, or changing the expected namespace replacement behavior in a way that breaks generated applications.

Adding optional folders, examples, or additional projects without disrupting the existing structure is normally a minor change.

## Configuration Surface

The following root configuration sections are part of the public surface.

|Section|Contract|
|:---|:---|
|`ConnectionStrings`|Contains named database connection strings used by the generated application.|
|`AllowedHosts`|Uses standard ASP.NET Core host filtering configuration.|
|`Serilog`|Defines default structured logging configuration.|
|`ProjectTemplate`|Contains application-owned template configuration.|

The following ProjectTemplate:* sections are part of the public surface.

|Section|Contract|
|:---|:---|
|`ProjectTemplate:DataProtection`|Persistent key-ring path and application isolation boundary.|
|`ProjectTemplate:ForwardedHeaders`|Reverse proxy and forwarded header behavior.|
|`ProjectTemplate:SecurityHeaders`|Security header and CSP behavior.|
|`ProjectTemplate:RateLimiting`|Global, fixed-window, and concurrency rate-limiting options.|
|`ProjectTemplate:RequestLogging`|Correlation ID and request logging behavior.|
|`ProjectTemplate:OpenTelemetry`|Tracing, metrics, service metadata, and OTLP exporter settings.|
|`ProjectTemplate:Authentication`|Cookie authentication and external provider configuration.|
|`ProjectTemplate:ClaimsTransformation`|Claim normalization and provider mapping behavior.|
|`ProjectTemplate:Authorization`|Application role and permission claim conventions.|
|`ProjectTemplate:DataAccess`|Data provider, connection string name, auditing defaults, and audit storage mode.|
|`ProjectTemplate:ApiVersioning`|API versioning defaults and supported readers.|

Breaking changes include renaming or removing documented keys, changing value types, changing defaults in a way that materially changes runtime behavior, or moving settings to a different section without compatibility support.

Adding optional keys with safe defaults is normally a minor change.

Correcting descriptions, examples, comments, or validation messages without behavior changes is normally a patch change.

### Data Access and Audit Storage Defaults

The `ProjectTemplate:DataAccess` section is part of the public configuration surface.

The following defaults are part of the stable runtime contract when data access is enabled:

| Setting | Default behavior |
|:---|:---|
| `Provider` | Uses `Sqlite` for local development. |
| `ConnectionStringName` | Resolves `ApplicationDatabase` unless a template option or configuration override selects another connection string name. |
| `Auditing:Enabled` | Enables audit record creation by default. |
| `Auditing:StorageMode` | Uses `Local` by default. |

Supported audit storage mode names are `Local`, `Outbox`, and `ExternalSink`. `Local` is the built-in implementation. `Outbox` and `ExternalSink` are extension modes that require a consuming application to register a custom `IApplicationAuditStore` implementation.

Changing the default local audit behavior, removing the local audit store, or changing the meaning of an audit storage mode is a public-surface change. Adding a new optional storage mode with a safe default is normally a minor change.

### Security Header Defaults

The `ProjectTemplate:SecurityHeaders` section is part of the public configuration surface.

The following defaults are part of the runtime contract when security headers are enabled:

| Header | Default behavior |
|:---|:---|
| `X-Content-Type-Options` | Emits `nosniff`. |
| `X-Frame-Options` | Emits `DENY`. |
| `Referrer-Policy` | Emits `strict-origin-when-cross-origin`. |
| `X-Permitted-Cross-Domain-Policies` | Emits `none`. |
| `Cross-Origin-Opener-Policy` | Emits `same-origin` when `EnableCrossOriginHeaders` is `true`. |
| `Cross-Origin-Resource-Policy` | Emits `same-origin` when `EnableCrossOriginHeaders` is `true`. |
| `Permissions-Policy` | Emits the configured `PermissionsPolicy` value when `EnablePermissionsPolicy` is `true`. |
| `Content-Security-Policy` | Emits the configured `ContentSecurityPolicy` value when `EnableContentSecurityPolicy` is `true`. |
| `X-XSS-Protection` | Not emitted because it is obsolete. |

Changing required default headers, changing documented default values, removing validation, or changing the meaning of documented security-header options is a breaking change after a stable release unless a compatibility path is provided.

Adding new optional security headers or new configuration options with safe defaults is normally a minor change.

## Route and Endpoint Conventions

The following endpoint conventions are part of the public surface.

|Endpoint or convention|Contract|
|:---|:---|
|`/health`|General application health endpoint.|
|`/health/ready`|Readiness endpoint for dependency-aware checks.|
|`/health/live`|Lightweight liveness endpoint.|
|`/Home/Error/500`|Non-development unhandled exception route.|
|`/Home/Error/{statusCode}`|Browser-oriented HTTP status code error route.|
|`/api/v{version}/<resource>`|Canonical API versioning route convention.|
|`X-API-Version`|Supported secondary API version reader.|

Changing or removing these conventions after a stable release is breaking unless the previous convention continues to work or a clear migration path is provided.

Adding new endpoints or optional route forms while preserving existing routes is normally a minor change.

## Middleware Ordering Contract

The middleware order is part of the public runtime contract because it affects proxy behavior, logging, error handling, security headers, routing, CORS, rate limiting, authentication, authorization, and endpoint mapping.

The committed order is:

&emsp;**1.** Forwarded headers<br />
&emsp;**2.** Structured request logging<br />
&emsp;**3.** Centralized error handling<br />
&emsp;**4.** Problem Details handling<br />
&emsp;**5.** Security headers<br />
&emsp;**6.** HTTPS redirection<br />
&emsp;**7.** Static files<br />
&emsp;**8.** Routing<br />
&emsp;**9.** CORS<br />
&emsp;**10.** Rate limiting<br />
&emsp;**11.** Authentication<br />
&emsp;**12.** Authorization<br />
&emsp;**13.** Controller and Razor Page endpoint mapping

Reordering, removing, or replacing middleware in a way that changes documented runtime behavior is breaking.

Adding optional middleware or documented extension points without disrupting the order is normally a minor change.

Fixing narrow bugs while preserving the documented order is normally a patch change.

## Container and Publishing Surface

The repository-maintained container image is part of the release evidence and distribution surface, but generated consumer applications may choose their own image names, registries, and deployment process.

The documented repository image is:
```
ghcr.io/asibackbone/netcoreapplicationtemplate
```

Stable semantic version tags publish:
```
ghcr.io/asibackbone/netcoreapplicationtemplate:<major>.<minor>.<patch>
ghcr.io/asibackbone/netcoreapplicationtemplate:<major>
ghcr.io/asibackbone/netcoreapplicationtemplate:latest
```

Prerelease tags publish the full version tag only and do not update `latest` or the major tag.

The documented local development image tag is:
```
projecttemplate-web:dev
```

The container listens on port `8080`.

Recommended probe paths are:
```
/health/live
/health/ready
```

### Repository Ownership and Registry Namespace Migration

A repository ownership transfer may require the canonical repository-maintained
container image to move to a registry namespace owned by the repository's new
organization.

Such a namespace migration is treated as a backward-compatible minor release
when all of the following remain true:

- The NuGet package identity, template identity, and generated application
  behavior remain unchanged.
- Generated applications do not depend on the repository-maintained container
  image as a runtime requirement.
- Previously published container images and immutable version tags remain
  available under the former registry namespace as historical compatibility
  artifacts.
- The new canonical registry namespace is clearly documented in release notes,
  deployment examples, and container-publishing documentation.
- Container runtime behavior, exposed ports, health probe contracts, and stable
  tag semantics are otherwise unchanged.

Historical images in the former namespace are not required to receive new
release tags after the ownership migration. New releases may be published only
under the canonical organization-owned namespace.

This ownership-transfer exception applies specifically when a repository
ownership change requires migration to a registry namespace controlled by the
new repository owner and the compatibility conditions above remain satisfied.

Outside that case, renaming the documented repository image, removing previously
published release artifacts, removing documented stable tag forms, changing
stable tag semantics, changing the default container port, or changing the
health probe contract is a breaking change unless a separate compatibility path
is provided.

Adding new image variants, additional tags, or additional examples while preserving the existing contract is normally a minor change.

## Internal-Only Changes

The following changes are normally internal-only when they do not affect generated consumer behavior or documented extension points:

- Refactoring private implementation details.
- Reorganizing repository-only documentation that is not included in generated output.
