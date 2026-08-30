# .NET Core Application Template Documentation

This site is the implementation and operational reference for the .NET Core Application Template (NCAT). It documents how to install and generate an application, what the generated scaffold contains, which runtime and security defaults are applied, how the application is configured and extended, and how it is deployed and operated.

NCAT is authoritative for its generated behavior, template options, configuration, middleware order, authentication and authorization defaults, data-access implementation, public surface, ADRs, release compatibility, and runtime contracts.

## At a Glance

| Area | Current reference |
|:---|:---|
| Current release | [Latest GitHub release](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/latest) |
| Supported platform | .NET 10.0 |
| Template package | [NetCoreApplicationTemplate on NuGet](https://www.nuget.org/packages/NetCoreApplicationTemplate) |
| Template short name | `netcoreapp-template` |
| Default authentication | Cookie authentication with authenticated-by-default routed endpoints |
| Default data provider | SQLite for local development |

## Start Here

1. **Install and generate an application:** [Install, Generate, and Template Options](articles/template-packaging.md) covers package installation, `dotnet new` generation, supported template options, and generated scaffold validation.
2. **Build, test, and run the repository:** [Build, Test, and First Run](articles/getting-started.md) covers prerequisites, local build/test commands, application startup, and local DocFX serving.
3. **Understand the generated application:** [Project Structure](articles/project-structure.md) explains the generated projects and responsibilities.
4. **Configure runtime behavior:** [Configuration](articles/configuration.md) documents the application-owned configuration surface.
5. **Understand the request pipeline:** [Middleware Pipeline](articles/middleware.md) records the runtime ordering contract and extension points.

## Generated Application Reference

Use these articles when you need the exact behavior of the scaffold rather than general ASP.NET Core guidance:

- [Authentication](articles/authentication.md)
- [Authorization](articles/authorization.md)
- [Error Handling](articles/error-handling.md)
- [Logging](articles/logging.md)
- [Telemetry](articles/telemetry.md)
- [Security Headers](articles/security-headers.md)
- [Forwarded Headers](articles/forwarded-headers.md)
- [Rate Limiting](articles/rate-limiting.md)
- [Health Checks](articles/health-checks.md)
- [API Versioning](articles/api-versioning.md)
- [Data Access](articles/data-access.md)

The [Public Surface and Compatibility Boundaries](articles/public-surface-v1.md) article records the generated configuration, routes, template symbols, middleware ordering, and other behavior consumers may rely on.

## Extensibility

- [Optional Application and Domain Layers](articles/optional-application-domain-layers.md)
- [Public Surface and Compatibility Boundaries](articles/public-surface-v1.md)

These references identify the intended growth points and the behavior that should remain stable when extending a generated application.

## Deployment & Operations

- [Deployment Notes](articles/deployment.md)
- [Docker Development](articles/docker.md)
- [Production Deployment Checklist](articles/production-deployment-checklist.md)
- [Runtime Readiness](articles/runtime-readiness.md)

## Architecture Decisions and API Reference

Repository-local design decisions remain first-class NCAT documentation because they explain why this implementation chose its concrete behavior.

- [Architecture Decision Records](adr/)
- [API Reference](api/)

## Release & Compatibility

Release and readiness evidence remains available after the core usage and implementation reference:

- [v1.0 Migration Guide](articles/v1-migration-guide.md)
- [Build Quality and Reproducibility](articles/build-quality.md)
- [Container Release Publishing](articles/container-publish.md)
- [GitHub Workflow](articles/github-workflow.md)
- [Test Coverage](coverage/)

## Learn the Architecture

For organization-level software architecture education, ASP.NET Core teaching, terminology, tutorials, tradeoff analysis, architectural comparisons, labs, and general secure-by-default guidance, use [ASI Backbone Learning](https://asibackbone.github.io/Learning/).

Learning provides broader educational context; it does **not** define NCAT runtime behavior. See [Documentation Ownership](articles/documentation-ownership.md) for the cross-repository ownership contract.
