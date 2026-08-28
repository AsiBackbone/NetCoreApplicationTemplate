# Documentation

This section contains implementation and operational documentation for the .NET Core Application Template. It is authoritative for NCAT-specific generated behavior, configuration, template options, runtime defaults, middleware order, authentication and authorization behavior, data-access implementation, deployment, extensibility, ADRs, public surface, releases, and compatibility guidance.

Broader architecture education belongs in [ASI Backbone Learning](https://asibackbone.github.io/Learning/), including general ASP.NET Core teaching, terminology, tutorials, tradeoffs, architectural comparisons, labs, and secure-by-default principles. The [Learning repository](https://github.com/AsiBackbone/Learning) contains the source for that educational material.

Learning may explain or compare patterns demonstrated by NCAT, but it does not define NCAT runtime contracts. When a topic needs both educational context and implementation detail, Learning owns the general lesson while NCAT documents the exact local decision and behavior.

Start with [Getting Started](getting-started.md), then use the navigation menu to browse the major documentation areas.

See [Documentation Ownership](documentation-ownership.md) for the cross-repository ownership matrix and contributor routing rules.

## Release readiness and compatibility

- [v1.0 Migration Guide](v1-migration-guide.md)
- [Public Surface](public-surface-v1.md)
- [Production Deployment Checklist](production-deployment-checklist.md)
- [Runtime Readiness](runtime-readiness.md)
- [Build Quality and Reproducibility](build-quality.md)
- [Container Release Publishing](container-publish.md)
- [Template Packaging](template-packaging.md)

## Application documentation

- [Project Structure](project-structure.md)
- [Optional Application and Domain Layers](optional-application-domain-layers.md)
- [Configuration](configuration.md)
- [Deployment Notes](deployment.md)
- [Docker Development](docker.md)
- [Middleware Pipeline](middleware.md)
- [Error Handling](error-handling.md)
- [Security Headers](security-headers.md)
- [Forwarded Headers](forwarded-headers.md)
- [Rate Limiting](rate-limiting.md)
- [Health Checks](health-checks.md)
- [API Versioning](api-versioning.md)
- [Logging](logging.md)
- [Telemetry](telemetry.md)
- [Authentication](authentication.md)
- [Production Authentication Hardening](authentication-hardening.md)
- [Authorization](authorization.md)
- [Data Access](data-access.md)
- [GitHub Workflow](github-workflow.md)
