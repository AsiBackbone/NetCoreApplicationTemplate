# .NET Core Application Template Documentation

Welcome to the documentation for the .NET Core Application Template.

This site is the implementation and operational reference for NCAT. It documents what the template currently generates, how its runtime pipeline is composed, which configuration and security defaults it applies, and how those concrete behaviors are operated and extended.

For organization-level software architecture education, ASP.NET Core teaching, terminology, tutorials, tradeoff analysis, architectural comparisons, labs, and general secure-by-default guidance, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/) and its [source repository](https://github.com/AsiBackbone/Learning).

Learning provides the broader educational context; it does **not** define NCAT runtime behavior. NCAT remains authoritative for its generated behavior, configuration, template options, middleware order, authentication and authorization defaults, data-access implementation, ADRs, release compatibility, and runtime contracts. See [Documentation Ownership](articles/documentation-ownership.md) for the cross-repository contract.

NCAT is a production-oriented ASP.NET Core application baseline. The default scaffold enables local cookie authentication and configures a fallback authorization policy that requires an authenticated user for routed endpoints without authorization metadata. Public routes are explicit anonymous exceptions. The `--authProvider none` variant is a deliberate opt-out that disables application authentication, cookie authentication, and authenticated fallback access.

Authentication establishes identity. Authorization determines whether that identity may access an endpoint or operation. NCAT also provides policy-based authorization, middleware ordering, request protection, observability, error handling, health checks, and EF Core data access patterns.

The template includes consistent defaults for:

- Application startup and middleware ordering
- Serilog structured logging
- Forwarded headers and reverse proxy support
- Security headers
- Rate limiting
- Centralized exception and status code handling
- Problem Details responses
- Health checks
- OpenTelemetry tracing and metrics
- Cookie authentication and optional external identity providers
- Authenticated-by-default routed endpoints and named authorization policies
- EF Core data access patterns
- GitHub Actions validation
- Package-based `dotnet new` template scaffolding

Use this documentation as the detailed reference for NCAT implementation and operational behavior. The root `README.md` provides the project summary and quick-start information; Learning provides broader architecture education and alternatives.

## Documentation Areas

- [Documentation Ownership](articles/documentation-ownership.md)
- [Getting Started](articles/getting-started.md)
- __Release Readiness and Compatibility__
  - [v1.0 Migration Guide](articles/v1-migration-guide.md)
  - [Public Surface](articles/public-surface-v1.md)
  - [Production Deployment Checklist](articles/production-deployment-checklist.md)
  - [Runtime Readiness](articles/runtime-readiness.md)
  - [Build Quality and Reproducibility](articles/build-quality.md)
  - [Container Release Publishing](articles/container-publish.md)
  - [Template Packaging](articles/template-packaging.md)
- __Application Basics__
  - [Project Structure](articles/project-structure.md)
  - [Configuration](articles/configuration.md)
  - [Deployment Notes](articles/deployment.md)
  - [Docker Development](articles/docker.md)
- __Middleware Pipeline__
  - [Middleware Pipeline](articles/middleware.md)
  - [Error Handling](articles/error-handling.md)
  - [Security Headers](articles/security-headers.md)
  - [Forwarded Headers](articles/forwarded-headers.md)
  - [Rate Limiting](articles/rate-limiting.md)
  - [Health Checks](articles/health-checks.md)
- [API Versioning](articles/api-versioning.md)
- __Observability__
  - [Logging](articles/logging.md)
  - [Telemetry](articles/telemetry.md)
- __Authentication and Authorization__
  - [Authentication](articles/authentication.md)
  - [Production Authentication Hardening](articles/authentication-hardening.md)
  - [Authorization](articles/authorization.md)
- [Data Access](articles/data-access.md)
- [GitHub Workflow](articles/github-workflow.md)
- [Test Coverage](https://AsiBackbone.github.io/NetCoreApplicationTemplate/coverage/index.html)
