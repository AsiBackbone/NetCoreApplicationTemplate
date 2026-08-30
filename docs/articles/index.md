# Template & Operations

This section is the task-oriented implementation reference for the .NET Core Application Template. Start with package installation and project generation, then use the generated-application reference for exact runtime behavior.

## Get Started

- [Install, Generate, and Template Options](template-packaging.md) — install the NuGet template package, create an application, choose supported options, and validate generated output.
- [Build, Test, and First Run](getting-started.md) — clone the repository, restore dependencies, build, test, run the web application, and serve the documentation locally.

## Generated Application Reference

- [Project Structure](project-structure.md)
- [Configuration](configuration.md)
- [Middleware Pipeline](middleware.md)
- [Authentication](authentication.md)
- [Production Authentication Hardening](authentication-hardening.md)
- [Authorization](authorization.md)
- [Error Handling](error-handling.md)
- [Logging](logging.md)
- [Telemetry](telemetry.md)
- [Security Headers](security-headers.md)
- [Forwarded Headers](forwarded-headers.md)
- [Rate Limiting](rate-limiting.md)
- [Health Checks](health-checks.md)
- [API Versioning](api-versioning.md)

### Data Access

- [Data Access Overview](data-access.md)
- [EF Core Diagnostics](ef-core-diagnostics.md)
- [EF Core Save Pipeline](ef-core-save-pipeline.md)
- [Audit Accountability Integration](audit-accountability-integration.md)
- [DbContext Audit State Isolation](dbcontext-audit-state-isolation.md)
- [Durable Audit-Completion Outbox](audit-completion-outbox.md)
- [Audit Reconciliation and Recovery](audit-reconciliation.md)

## Extensibility

- [Optional Application and Domain Layers](optional-application-domain-layers.md)
- [Public Surface and Compatibility Boundaries](public-surface-v1.md)

## Deployment & Operations

- [Deployment Notes](deployment.md)
- [Docker Development](docker.md)
- [Production Deployment Checklist](production-deployment-checklist.md)
- [Runtime Readiness](runtime-readiness.md)

## Release & Compatibility

- [v1.0 Migration Guide](v1-migration-guide.md)
- [Build Quality and Reproducibility](build-quality.md)
- [Container Release Publishing](container-publish.md)
- [GitHub Workflow](github-workflow.md)

Architecture Decision Records and generated API documentation are available as first-class top-level sections of the site.

## Documentation Scope

NCAT is authoritative for its template installation, options, generated structure, exact runtime defaults, configuration, middleware order, authentication and authorization behavior, data-access implementation, deployment, extensibility, ADRs, public surface, releases, compatibility, and operational guidance.

Broader architecture education belongs in [ASI Backbone Learning](https://asibackbone.github.io/Learning/). Learning may explain or compare patterns demonstrated by NCAT, but it does not define NCAT runtime contracts. See [Documentation Ownership](documentation-ownership.md) for the cross-repository ownership matrix and contributor routing rules.
