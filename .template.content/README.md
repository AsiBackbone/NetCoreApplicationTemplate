# ProjectTemplate

ProjectTemplate is a scaffolded ASP.NET Core application created from the `netcoreapp-template` template.

The generated solution includes a production-oriented baseline for common web application infrastructure concerns:

- ASP.NET Core web application structure.
- Centralized middleware organization.
- Structured Serilog logging.
- Security headers.
- Forwarded headers and reverse proxy support.
- Rate limiting.
- Centralized exception and status-code handling.
- Problem Details responses.
- Cookie authentication and authenticated-by-default routed endpoints in the default scaffold.
- Persistent ASP.NET Core Data Protection keys for authentication cookies and antiforgery tokens.
- Named role and permission policies for policy-based authorization.
- EF Core-ready data access structure.
- Health checks.
- Docker and Docker Compose support.
- Baseline automated tests.

## Prerequisites

- .NET SDK 10.0 or later.
- Docker Desktop or a compatible container runtime, only if you plan to use Docker Compose.

The generated project includes `global.json` so SDK selection is consistent across local development and CI validation.

## Template Options Used

This project was generated from the `netcoreapp-template` template.

The template supports these scaffold options:

| Option | Default | Supported values | Description |
|:---|:---|:---|:---|
| `--authProvider` | `cookie` | `cookie`, `none` | Selects either the default cookie-authentication and authenticated-fallback posture or the explicit authentication-disabled opt-out. |
| `--dbProvider` | `sqlite` | `sqlite`, `sqlserver`, `none` | Selects the generated data access mode. |
| `--skipRestore` | `false` | `true`, `false` | Skips the post-create NuGet restore action when set to `true`. |

Both authentication variants retain the template's infrastructure controls, including structured logging, centralized error handling, health checks, security headers, and rate limiting. Endpoint access differs materially between the variants and must be reviewed explicitly.

### Default scaffold

The default scaffold enables local cookie authentication, requires authenticated access for routed endpoints through the fallback authorization policy, and uses SQLite development data access:

```powershell
dotnet new netcoreapp-template --name ProjectTemplate
```

Equivalent explicit options:

```powershell
dotnet new netcoreapp-template `
  --name ProjectTemplate `
  --authProvider cookie `
  --dbProvider sqlite
```

### Authentication-disabled SQL Server scaffold

A common non-default scaffold explicitly opts out of application authentication and selects SQL Server configuration:

```powershell
dotnet new netcoreapp-template `
  --name ProjectTemplate `
  --authProvider none `
  --dbProvider sqlserver
```

When `--authProvider none` is used, application authentication, cookie authentication, and the authenticated fallback policy are disabled in the generated `appsettings.json`.

This means unannotated routed endpoints are public until the consuming application adds another authentication mechanism and authorization posture. Choose this variant only as a deliberate architectural decision, not as a neutral equivalent of the default scaffold.

Authentication and authorization tests that intentionally exercise protected endpoints may need to enable test authentication through in-memory test configuration.

When `--dbProvider sqlserver` is used, the generated data access configuration selects `SqlServer` and `ApplicationSqlServer`. The generated `ConnectionStrings:ApplicationSqlServer` value is a local development example and should be replaced through environment-specific configuration before production use.

EF Core migrations are provider-specific. SQL Server scaffolds therefore omit the template's SQLite `Data/Migrations` history and its SQLite migration-specific tests. Generate a fresh SQL Server migration before applying database updates:

```powershell
dotnet ef migrations add InitialCreate `
  --project src/ProjectTemplate.Infrastructure `
  --startup-project src/ProjectTemplate.Web `
  --context ApplicationDbContext `
  --output-dir Data/Migrations
```

If an existing generated application is switched from SQLite to SQL Server before deployment, delete its SQLite migration folder and regenerate the migration history for SQL Server. Do not delete or replace migrations that have already been applied to a shared or production database without an explicit migration and data-preservation plan.

When `--dbProvider none` is used, the generated data access configuration selects `None`. EF Core application data access services are not registered, and no data access connection string is required unless the consuming application adds its own persistence strategy.

## Restore

```powershell
dotnet restore
```

## Build

```powershell
dotnet build --configuration Release
```

## Test

```powershell
dotnet test --configuration Release
```

## Run Locally

```powershell
dotnet run --project src/ProjectTemplate.Web
```

The local development profile may choose an available HTTPS or HTTP port depending on launch settings and environment. Docker Compose uses the fixed local port documented below.

## Run with Docker Compose

```powershell
docker compose up --build
```

The Docker-hosted application is available at:

```text
http://localhost:8080
```

Health endpoints are available at:

```text
http://localhost:8080/health
http://localhost:8080/health/ready
http://localhost:8080/health/live
```

## Docker Environment Overrides

The generated scaffold includes `.env.example` for optional local Docker Compose overrides.

To customize local Compose values:

```powershell
Copy-Item .env.example .env
```

Do not commit `.env` files that contain local or production environment-specific values.

## Configuration Notes

The generated `appsettings.json` includes baseline configuration for:

- Data Protection key persistence and application isolation.
- Forwarded headers.
- Security headers.
- Rate limiting.
- Request logging.
- OpenTelemetry.
- Authentication and external provider placeholders.
- Claims transformation.
- Authorization.
- Data access.
- API versioning.

### Authentication and authorization terminology

- **Authentication** establishes the caller's identity.
- **Authorization** determines whether that identity may access an endpoint or operation.
- The **default authorization policy** is used when authorization is requested without a named policy.
- The **fallback authorization policy** applies to routed endpoints that contain no authorization metadata.
- **Explicit anonymous access** uses `[AllowAnonymous]`, `.AllowAnonymous()`, or equivalent metadata to exempt a route from the fallback policy.
- **Policy-based authorization** adds role, permission, claim, or custom requirements beyond the authenticated-user baseline.

### Authentication

The default scaffold enables application authentication with local cookie authentication. External providers such as OpenID Connect, SAML2, Microsoft, Google, and GitHub are present as disabled placeholders.

Enable external providers intentionally and store provider-specific values outside committed configuration. Authentication establishes identity; it does not by itself grant endpoint access.

ASP.NET Core Data Protection keys default to `DataProtection-Keys` under the application content root. Keep this directory durable and access-restricted. All replicas of the same application must share the key ring and use the same `ProjectTemplate:DataProtection:ApplicationName`; changing either invalidates existing authentication cookies and antiforgery tokens. The generated Docker Compose file supplies a persistent named volume for the container key-ring path.

### Endpoint access contract

The default authenticated scaffold requires an authenticated user for routed endpoints unless the endpoint is deliberately marked anonymous.

The intentionally anonymous routed endpoint categories are:

- `GET /Account/Login` for entering the authentication flow.
- `GET /External/Challenge` for starting a configured external-provider challenge.
- `GET /Account/AccessDenied` and `GET /Home/Error/{statusCode?}` so failure handling cannot redirect recursively.
- `/health`, `/health/live`, and `/health/ready` for infrastructure probes.

`POST /Account/Logout` explicitly requires an authenticated user and a valid anti-forgery token. The starter Razor Page, sample API, and newly added routed endpoints inherit authenticated access unless an explicit public-access decision is made.

External-provider callback paths are handled by their authentication middleware rather than application controller authorization. Static files are served by static-file middleware before routing; do not place sensitive content under `wwwroot`.

Health endpoints are anonymous at the application layer so deployment probes do not depend on browser authentication. Restrict production reachability through ingress, firewall, reverse-proxy, load-balancer, or service-mesh policy as appropriate.

### Forwarded headers and client-IP rate limiting

Outside Development, startup fails when forwarded client-IP processing and rate limiting are enabled without a configured `KnownProxies` or `KnownNetworks` trust boundary. Configure the narrow proxy addresses or ingress CIDR used by the deployment, or disable forwarded headers when the application is reached directly. This prevents all clients behind an untrusted proxy from silently sharing the proxy's rate-limit partition.

### Data access

The default scaffold selects SQLite with `ConnectionStrings:ApplicationDatabase`.

SQL Server scaffolds select `ConnectionStrings:ApplicationSqlServer`.

SQL Server scaffolds intentionally start without migrations. Generate the initial migration after setting the real SQL Server connection string and reviewing the model. SQLite scaffolds retain the template's checked-in SQLite migration history.

The EF Core model includes baseline optimistic concurrency support for entities that inherit from the shared data entity base type. Concurrency conflicts are surfaced through EF Core rather than silently overwriting stale updates.

The scaffold does not run EF Core migrations automatically during startup. Apply migrations intentionally as part of local setup or deployment.

When data access is disabled with `--dbProvider none`, EF Core services are not registered and migration commands are not applicable unless the consuming application adds its own persistence layer.

### Health checks

Use `/health/live` for process liveness and `/health/ready` for dependency-aware deployment readiness.

The baseline readiness endpoint intentionally includes only checks explicitly tagged for readiness. Add database, cache, or external dependency checks only when they should remove the instance from normal traffic rotation.

## Generated Content

This scaffold intentionally includes:

- Solution and project files.
- Source projects under `src/`.
- Baseline tests under `tests/`.
- Docker support files.
- Local configuration examples.
- `LICENSE.txt`.
- `ASSETS-LICENSES.md`.
- This consumer-oriented `README.md`.

This scaffold intentionally excludes repository-maintainer files such as GitHub issue templates, release workflows, DocFX documentation source, ADRs, contribution policy, security policy, package README, changelog, release-management instructions, and repository badges.

## Upstream References

The upstream template package and documentation can be used for deeper implementation guidance:

- NuGet package: `NetCoreApplicationTemplate`
- GitHub repository: `AsiBackbone/NetCoreApplicationTemplate`
- Published documentation: `https://asibackbone.github.io/NetCoreApplicationTemplate/`


## License

This generated project includes the template license in `LICENSE.txt`.

Third-party asset and notice information is included in `ASSETS-LICENSES.md`.

Review both files before publishing or redistributing an application created from this scaffold.
