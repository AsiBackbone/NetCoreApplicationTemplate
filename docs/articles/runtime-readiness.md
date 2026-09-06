# Runtime Readiness Baseline

This page summarizes the runtime foundations in the current stable readiness baseline across observability, error handling, authentication, authorization, and request protection.

## Baseline Registration

The application registers the runtime baseline during startup in `Program.cs`:

- Serilog and structured request logging.
- Forwarded headers.
- Security headers.
- Rate limiting.
- OpenTelemetry tracing and metrics.
- Centralized Problem Details.
- Authentication and authorization.
- EF Core-ready data access.

The middleware pipeline applies forwarded headers early, then request logging, centralized error handling, Problem Details, security headers, HTTPS redirection, static files, routing, CORS, rate limiting, authentication, authorization, controllers, and Razor Pages.

## Observability Baseline

OpenTelemetry is included as the tracing and metrics baseline.

Default behavior:

- `ProjectTemplate:OpenTelemetry:Enabled` is `true`.
- Tracing is enabled.
- Metrics are enabled.
- ASP.NET Core instrumentation is enabled.
- HTTP client instrumentation is enabled.
- OTLP export is disabled until an endpoint is configured.

The template does not map a direct `/metrics` scraping endpoint by default. Add scraping deliberately and review its authorization and network exposure.

## W3C Trace Context

The template uses ASP.NET Core and OpenTelemetry defaults for request activity creation and propagation. Problem Details responses include `traceId` from `Activity.Current?.Id` when available, falling back to the ASP.NET Core request trace identifier.

## Structured Logging Baseline

Serilog is the structured logging provider.

| Source | Default level |
|:---|:---|
| Application | `Information` |
| `Microsoft` | `Warning` |
| `Microsoft.AspNetCore` | `Warning` |
| `Microsoft.AspNetCore.Hosting` | `Warning` |
| `Microsoft.AspNetCore.Mvc` | `Warning` |
| `Microsoft.AspNetCore.Routing` | `Warning` |
| `System` | `Warning` |

Console and rolling file sinks include timestamp, level, source context, request ID, request path, and exception details when present. Sensitive values such as credentials, cookies, authentication tokens, request bodies, and response bodies are not logged by default.

## Problem Details Baseline

Centralized Problem Details support is registered through `AddApplicationProblemDetails` and applied through `UseProblemDetails`.

API, AJAX, and JSON-oriented requests receive Problem Details responses. Outside Development, server errors use generic detail text and do not expose stack traces, exception messages, connection details, secret values, or internal implementation details. Browser-oriented requests use the shared error-page flow.

## Authentication Baseline

Authentication establishes the caller's identity. Application authentication is enabled by default with a cookie session handler:

- `ProjectTemplate:Authentication:Enabled` is `true`.
- The default authenticate, challenge, and sign-in schemes use `Cookies`.
- The cookie handler can store an authenticated session after a sign-in flow succeeds.
- External OpenID Connect, SAML2, Microsoft, Google, and GitHub providers are disabled until explicitly configured.
- Enabled external providers are validated during startup and fail fast when required values are missing.

The default scaffold does not include local user accounts, a credential form, a seeded user, or an enabled sign-in provider. Cookie authentication does not verify credentials or provide a login path by itself, so protected routes remain unavailable until the consuming application enables an external provider or supplies its own identity flow. External provider secrets and real identity-provider metadata must be supplied through protected configuration sources.

## Authorization Baseline

Authorization determines whether an authenticated identity may access an endpoint or operation.

The default scaffold is closed by default for routed endpoints. `ProjectTemplate:Authorization:RequireAuthenticatedUserByDefault` is `true`, so the fallback authorization policy requires authentication for routed endpoints without authorization metadata. Explicit anonymous metadata exempts only reviewed public routes.

The default authorization policy and fallback authorization policy serve different purposes: the default policy applies when authorization is requested without a named policy, while the fallback policy applies when a routed endpoint has no authorization metadata.

Named policy-based authorization is available for stronger requirements:

| Policy | Purpose |
|:---|:---|
| `application.AuthenticatedUser` | Requires an authenticated user. |
| `application.Role.Administrator` | Requires an authenticated user with a configured administrator role claim. |
| `application.Permission.ManageApplication` | Requires an authenticated user with a configured manage-application permission claim. |

The `--authProvider none` scaffold explicitly disables application authentication, cookie authentication, and the authenticated fallback policy. Unannotated routed endpoints are public in that variant until another posture is configured.

## Rate Limiting Baseline

Rate limiting is enabled and configurable by default:

- A global fixed-window limiter is available for baseline request protection.
- Named fixed-window and concurrency policies are available for endpoint-specific protection.
- Rejected requests return HTTP `429 Too Many Requests` and are logged.

Production applications should tune limits based on traffic profile, endpoint sensitivity, proxy behavior, authenticated versus anonymous traffic, and expensive I/O.

## Deployment-Owned Decisions

The runtime baseline does not complete deployment-specific decisions. Production owners must still select and configure:

- OTLP collectors and metrics exporters.
- Production log sinks and retention.
- Identity-provider registrations and secrets.
- Network exposure for explicitly anonymous health probes.
- Rate-limit values and partitioning.
- Any intentional opt-out from authenticated fallback access.

Template-level implementation gaps should be tracked separately from deployment tuning decisions.
