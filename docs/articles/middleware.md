# Middleware Pipeline

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application centralizes standard middleware ordering through `UseApplicationPipeline()` so `Program.cs` remains focused on application startup and service registration.

The detailed architecture decision is recorded in [ADR-0002: Use Centralized Application Middleware Pipeline](../adr/0002-use-centralized-application-middleware-pipeline.md).


## Implementation Locations

- Exact generated order: [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs)
- Startup composition: [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Program.cs)
- Architecture decision: [ADR-0002](../adr/0002-use-centralized-application-middleware-pipeline.md)

## Baseline Order

The pipeline order is:

1. Forwarded headers
2. Structured request logging
3. Centralized error handling
4. Problem Details handling
5. Security headers
6. HTTPS redirection
7. Static files
8. Routing
9. CORS
10. Rate limiting
11. Authentication
12. Authorization
13. Controller and Razor Page endpoint mapping

This order keeps proxy correction early, request logging close to the beginning of the request, error handling ahead of most application behavior, and endpoint-specific features such as CORS and rate limiting after routing.

## Order-Sensitive Invariants

The pipeline is intentionally ordered around a few security and behavior invariants.

| Pipeline area | Why the order matters |
| --- | --- |
| Forwarded headers | Reverse proxy correction must happen before middleware reads scheme, host, path base, remote IP, or client IP. This keeps request logging, HTTPS redirection, generated URLs, and rate-limiting decisions aligned with the externally observed request. |
| Request logging | Logging runs after forwarded-header correction so logs capture the corrected request identity, but still early enough to wrap most downstream behavior. |
| Error handling and Problem Details | Centralized exception and status-code handling should wrap normal application behavior so failures collapse into consistent, safe responses. |
| Security headers | Security headers should be applied before ordinary response-producing middleware so normal responses and many error responses carry the configured hardening headers. |
| HTTPS redirection | HTTPS enforcement should run before routing and endpoint execution, after forwarded headers have corrected proxy-terminated HTTPS requests. |
| Static files | Static assets are served before routing using the conventional Razor Pages/MVC placement. Moving them behind authentication or authorization changes static asset exposure and should be intentional. |
| Routing | Routing establishes endpoint metadata used by later endpoint-aware middleware. |
| CORS | CORS runs after routing and before authentication/authorization so endpoint metadata can influence CORS behavior and preflight requests are not accidentally converted into auth failures. |
| Rate limiting | Rate limiting runs after routing so endpoint-specific policies can participate. Moving it before routing changes the policy surface toward global limiting only. |
| Authentication and authorization | Authentication must run before authorization so policy checks evaluate the authenticated principal rather than an anonymous request. |
| Endpoint mapping | Controller and Razor Page endpoint mapping remains at the end of the template-owned pipeline. |

## Health Checks

`Program.cs` maps health-check endpoints with `MapApplicationHealthChecks()` immediately after `UseApplicationPipeline()`.

Health-check mapping is kept visible in startup because production deployments often treat health endpoints differently from UI or business endpoints. Consumers that adjust health checks should preserve the intended access model and avoid accidentally putting liveness/readiness checks behind unrelated UI or business authorization requirements unless that is deliberate.

## Adding Custom Middleware

When adding custom middleware, use the invariant that describes the behavior the middleware depends on:

- Middleware that depends on corrected proxy information should run after forwarded headers.
- Middleware that should be covered by centralized exception handling should run after the error-handling middleware.
- Middleware that depends on endpoint metadata should run after routing.
- Middleware that makes identity or policy decisions should normally run after authentication and before or during authorization-aware endpoint execution.
- Middleware that emits response headers should be reviewed against redirects, static files, error responses, and endpoint responses to confirm where those headers should appear.

Pipeline changes that alter request identity, error handling coverage, security-header coverage, CORS preflight behavior, rate-limiting policy selection, authorization behavior, or health-check reachability should be treated as behavior changes and reviewed accordingly.

## Contract References

The generated order is authoritative in `PipelineExtensions.cs`. Order-sensitive behavior is exercised by the focused forwarded-header, security-header, rate-limiting, authentication/authorization, and health-check tests.

## Learn the Pattern

For general middleware-ordering theory and alternatives, see [Middleware Ordering Changes Behavior](https://asibackbone.github.io/Learning/aspnetcore/middleware-ordering-changes-behavior.html).
