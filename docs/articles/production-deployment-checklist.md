# Production Deployment Checklist

Use this checklist before deploying an application generated from the .NET Core Application Template to a production environment.

This checklist is intentionally operator-focused. It does not replace organization-specific security, compliance, change-management, or infrastructure review.

## 1. Release and Build Readiness

```text
[ ] Application source is built from the intended commit, branch, or release tag.
[ ] Release configuration build succeeds.
[ ] Tests pass.
[ ] CI status is green.
[ ] Package, image, or deployment artifact version is identifiable.
[ ] Generated application README matches the deployed artifact behavior.
[ ] Release notes identify any configuration, deployment, or migration requirements.
```

Related docs:
- [Build Quality and Reproducibility](build-quality.md)
- [GitHub Workflow](github-workflow.md)
- [Template Packaging](template-packaging.md)
- [v1.0 Migration Guide](v1-migration-guide.md)

## 2. Hosting and Environment

```text
[ ] `ASPNETCORE_ENVIRONMENT` is set to `Production`.
[ ] Public host names are known.
[ ] `AllowedHosts` is configured for expected production hosts.
[ ] HTTPS is enforced at the correct layer.
[ ] TLS termination location is documented.
[ ] Deployment slot, region, and environment names are documented.
[ ] Platform-specific startup command or container entry point is verified.
[ ] Startup configuration validation has been tested with the production configuration source.
```

Related docs:
- [Deployment Notes](deployment.md)
- [Configuration](configuration.md)

## 3. Reverse Proxy and Forwarded Headers

```text
[ ] Reverse proxy, load balancer, ingress, or gateway topology is documented.
[ ] Forwarded headers are enabled only when required.
[ ] Trusted proxies or known networks are configured.
[ ] Forward limit matches the deployment topology.
[ ] Host forwarding is enabled only when required.
[ ] Public HTTPS scheme is preserved correctly.
[ ] Redirects do not loop behind the proxy.
[ ] Authentication callback URLs use the public HTTPS origin.
```

Related docs:
- [Forwarded Headers](forwarded-headers.md)
- [Deployment Notes](deployment.md)

## 4. Middleware Order

```text
[ ] Forwarded headers run before components that depend on scheme, host, or client IP.
[ ] Request logging runs early enough to capture useful request context.
[ ] Centralized error handling is registered before endpoint execution.
[ ] Security headers are applied before responses are sent.
[ ] HTTPS redirection behavior is compatible with proxy/TLS termination.
[ ] Static files are served before endpoint routing when expected.
[ ] Routing, CORS, rate limiting, authentication, and authorization order is preserved.
[ ] Controller and Razor Page endpoint mapping remains last.
```

Related docs:
- [Middleware Pipeline](middleware.md)
- [Public Surface v1.0](public-surface-v1.md)

## 5. Logging
```text
[ ] Production log sinks are configured.
[ ] Log retention expectations are defined.
[ ] Request correlation IDs are available.
[ ] Sensitive values are not logged.
[ ] Authentication secrets, tokens, connection strings, and provider secrets are not logged.
[ ] Warning and error events are routed to the expected monitoring destination.
[ ] Startup failures are observable.
```

Related docs:

- [Logging](logging.md)
- [Telemetry](telemetry.md)
- [Configuration](configuration.md)

## 6. Error Handling and Problem Details

```text
[ ] Non-development exception handling route is active.
[ ] Status code pages are configured as expected.
[ ] API error responses use Problem Details where appropriate.
[ ] Browser error pages do not expose sensitive implementation details.
[ ] 404, 500, and validation-style responses are smoke tested.
[ ] Development exception details are not enabled in production.
```

Related docs:

- [Error Handling](error-handling.md)
- [Middleware Pipeline](middleware.md)

## 7. Security Headers

```text
[ ] Security headers are enabled.
[ ] Content Security Policy is reviewed against deployed assets.
[ ] Script, style, image, connection, and frame directives are intentional.
[ ] `frame-ancestors` behavior matches embedding requirements.
[ ] Permissions Policy is reviewed.
[ ] Referrer Policy is reviewed.
[ ] Cross-origin header behavior is tested with authentication and static assets.
[ ] No broad wildcard sources are used unless intentionally accepted.
```

Related docs:

- [Security Headers](security-headers.md)
- [Configuration](configuration.md)

## 8. Rate Limiting

```text
[ ] Global rate limit values match expected traffic.
[ ] Queue limits are intentionally chosen.
[ ] Authentication, API, administrative, or expensive endpoints have appropriate limits.
[ ] Health checks and infrastructure probes are not accidentally blocked.
[ ] Expected client behavior after `429 Too Many Requests` is documented.
[ ] Logs or metrics can identify rate-limit pressure.
[ ] Limits will be revisited after real production traffic is observed.
```

Related docs:

- [Rate Limiting](rate-limiting.md)
- [Deployment Notes](deployment.md)

## 9. Health Checks

```text
[ ] `/health` behavior is understood.
[ ] `/health/live` is configured for liveness probes.
[ ] `/health/ready` is configured for readiness probes and includes the database, cache, queue, external service, or other dependency checks required before the service receives normal production traffic.
[ ] Infrastructure probe paths and methods match application behavior.
[ ] Health responses do not expose sensitive dependency details publicly.
[ ] Load balancer or orchestrator health behavior is tested.
```

Readiness scope:

The template provides the readiness endpoint shape, but each generated application must register the dependency checks that define production readiness for that service. Do not treat the default readiness endpoint as proof that database, cache, queue, or external service dependencies are available unless those checks have been explicitly registered and included in readiness.

Related docs:

- [Health Checks](health-checks.md)
- [Deployment Notes](deployment.md)

## 10. Telemetry

```text
[ ] OpenTelemetry settings are reviewed.
[ ] Service name and environment metadata are correct.
[ ] OTLP endpoint is configured when telemetry export is enabled.
[ ] Metrics and traces are routed to the expected backend.
[ ] Telemetry does not include sensitive values.
[ ] Sampling/export behavior is appropriate for production volume.
```

Related docs:

- [Telemetry](telemetry.md)
- [Logging](logging.md)

## 11. Authentication and Authorization

```text
[ ] Cookie settings are production appropriate.
[ ] External provider client IDs are configured.
[ ] External provider client secrets are supplied by secret storage.
[ ] Redirect and callback URLs match the public HTTPS origin.
[ ] Claims transformation behavior is reviewed.
[ ] Role and permission claim conventions are documented.
[ ] Unauthorized and forbidden behavior is tested.
[ ] Sign-in and sign-out flows are smoke tested.
[ ] Required authorization claim types, administrator roles, and management permissions are configured.
```

Related docs:

- [Authentication](authentication.md)
- [Authorization](authorization.md)
- [Configuration](configuration.md)

## 12. Data Access

```text
[ ] Production database provider is selected intentionally.
[ ] Production connection string comes from secret storage or protected environment configuration.
[ ] Database account uses least privilege.
[ ] EF Core migrations are reviewed before production execution.
[ ] Migration execution is an explicit deployment step.
[ ] Database backup/restore approach is understood before schema changes.
[ ] Connection resiliency and timeout expectations are reviewed.
[ ] Local development database settings are not used in production.
[ ] Missing or invalid provider/connection string settings fail startup before deployment approval.
```

Related docs:

- [Data Access](data-access.md)
- [Configuration](configuration.md)

## 13. Container Deployment

Use this section when deploying with containers.

```text
[ ] Image tag matches the intended release.
[ ] Container listens on the expected port.
[ ] Environment variables are injected correctly.
[ ] Secrets are not baked into the image.
[ ] Container health probe uses the documented path.
[ ] Container logs are captured by the platform.
[ ] Docker Compose or orchestrator configuration has been validated.
```

Related docs:

- [Docker Development](docker.md)
- [Container Release Publishing](container-publish.md)
- [Deployment Notes](deployment.md)

## 14. Final Production Smoke Test

Before completing deployment, verify:

```text
[ ] Application starts successfully.
[ ] Home page or expected root endpoint responds.
[ ] `/health/live` responds as expected.
[ ] `/health/ready` responds as expected.
[ ] A known 404 path returns the expected response.
[ ] A known API route returns the expected response.
[ ] Static assets load.
[ ] Authentication flow works, if enabled.
[ ] Logs contain expected startup and request entries.
[ ] No secrets appear in logs or responses.
```

## 15. Rollback and Hotfix Readiness

```text
[ ] Previous deployable version is available.
[ ] Database rollback or forward-fix strategy is understood.
[ ] Configuration changes are tracked.
[ ] Hotfix branch process is understood.
[ ] Corrective patch release process is understood.
[ ] Documentation correction process is understood.
```

Related docs:

- [Release Checklist](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/RELEASE.md)
- [v1.0 Migration Guide](v1-migration-guide.md)

## Summary

A production deployment is ready when the generated application has been validated as an application, as a deployment artifact, and as an operator-owned service.

The template provides a production-oriented baseline, but each environment must still make explicit decisions about hosting, trust boundaries, secrets, logging, telemetry, rate limits, health checks, authentication, and data access.
