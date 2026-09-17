# Rate Limiting

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application includes baseline ASP.NET Core rate limiting support to help protect applications from accidental request floods, scraping, repeated automated requests, and concurrency-heavy operations.

Rate limiting is registered through the application service extension:

```csharp
builder.Services.AddApplicationRateLimiting(builder.Configuration, builder.Environment);
```

The middleware is applied in the standard application pipeline:

```csharp
app.UseRateLimiter();
```

`UseRateLimiter()` is intentionally placed after routing so endpoint-specific rate limiting policies can be applied, and before endpoint execution so requests can be rejected before reaching controllers, Razor Pages, or minimal API handlers.


## Implementation Locations

- Limiter registration/policies/rejection/partitioning: [`RateLimitingServiceExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/RateLimitingServiceExtensions.cs)
- Option models: [`src/ProjectTemplate.Web/Options`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/src/ProjectTemplate.Web/Options)
- Named policy constants: [`ApplicationRateLimitingPolicyNames.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Constants/ApplicationRateLimitingPolicyNames.cs)
- Generated defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)

## Default Behavior

The application supports:

- A global fixed-window limiter for baseline request protection.
- A named fixed-window policy for endpoint-specific use.
- A named concurrency policy for sensitive or resource-heavy operations.
- Problem Details rejection responses for API-shaped requests and short plain-text responses for other requests.
- `429 Too Many Requests` responses when limits are exceeded.
- Logging for rejected requests that honors the request-logging remote IP address privacy option.
- Throttled warning logging when client IP partitioning must use the unknown-client fallback path.

Rejected requests always return `429 Too Many Requests`. When the limiter reports a retry interval, the response includes a `Retry-After` header in seconds.

API-shaped requests, classified the same way as the rest of centralized error handling (a path under `/api`, an `X-Requested-With: XMLHttpRequest` header, or an `Accept` header that includes JSON), receive an `application/problem+json` response written through `IProblemDetailsService`. The shared Problem Details customization adds the same `instance`, `traceId`, `spanId`, `requestId`, and `correlationId` members used by other error responses:

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Too many requests were received. Please try again later.",
  "instance": "/api/orders",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "requestId": "0HNL9ADUFCPUT:00000009",
  "correlationId": "0HNL9ADUFCPUT:00000009"
}
```

Other requests receive a short `text/plain` body with the same detail message. `HEAD` requests receive no body.

Rejections intentionally do not re-execute the browser error page. A rate limiter protects the application under load, so a rejection must stay inexpensive; rendering a Razor view for every rejected request would work against that protection.

### Rejection Logging

Each rejection writes a warning with event ID `6100` containing the method, path, endpoint, retry interval, and trace identifier. The remote IP address is included only when `ProjectTemplate:RequestLogging:IncludeRemoteIpAddress` is `true`, the same option that governs request logs; otherwise it is recorded as null. See [Logging](logging.md).

## Configuration

Rate limiting values can be configured from `appsettings.json`:

```json
"ProjectTemplate": {
  "RateLimiting": {
    "Enabled": true,
    "UseGlobalLimiter": true,
    "UseSharedUnknownClientPartition": true,
    "UnknownClientPartitionKey": "unknown-client",
    "GlobalFixedWindow": {
      "PermitLimit": 60,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "FixedWindowPolicy": {
      "PermitLimit": 60,
      "WindowSeconds": 60,
      "QueueLimit": 0
    },
    "ConcurrencyPolicy": {
      "PermitLimit": 10,
      "QueueLimit": 0
    }
  }
}
```

These defaults are intentionally conservative and should be reviewed before production use.

## Client Partitioning and Unknown-Client Fallback

The fixed-window limiters partition clients by `HttpContext.Connection.RemoteIpAddress`. The template intentionally relies on ASP.NET Core Forwarded Headers Middleware to correct that value when the application runs behind a trusted reverse proxy, load balancer, ingress controller, CDN, or gateway.

The rate limiter does **not** parse or trust raw `X-Forwarded-For` values directly. Raw forwarded headers are client-controllable unless ASP.NET Core has first validated them through trusted `KnownProxies` or `KnownNetworks` configuration.

Outside Development, the generated application fails startup by default when forwarded client-IP processing and rate limiting are enabled without either trust setting. This prevents all users behind an ingress from silently sharing the ingress address's rate-limit bucket.

When `RemoteIpAddress` is unavailable, the default behavior is to place every unresolved client in one shared fallback partition named by `UnknownClientPartitionKey`. Unresolved clients therefore remain rate limited, and the fixed-window limiters fail closed rather than open.

The shared partition is a safety floor, not a substitute for client identity. Because all unresolved clients draw from the same permits, heavy traffic from one unresolved client can cause `429` responses for other unresolved clients. When that happens, resolve client identity rather than loosening the fallback.

Setting `UseSharedUnknownClientPartition` to `false` gives each unresolved request its own partition based on `UnknownClientPartitionKey` and the request trace identifier:

```json
"RateLimiting": {
  "UseSharedUnknownClientPartition": false,
  "UnknownClientPartitionKey": "unknown-client"
}
```

Because every request receives a new partition, this mode **effectively disables client rate limiting for unresolved clients**. Use it only when an upstream gateway, ingress, or platform already enforces rate limits for that traffic.

| `UseSharedUnknownClientPartition` | Unresolved-client behavior | Risk |
|:---|:---|:---|
| `true` (default) | All unresolved clients share one bucket | Unrelated unresolved clients can throttle each other |
| `false` | Each unresolved request gets its own bucket | Unresolved clients are not rate limited by the application |

A warning (event ID `6002`) is emitted when this fallback path is used. The warning is written at most once per minute per rate-limiting registration and includes `SuppressedWarningCount`, the number of fallback occurrences since the previous warning, so a deployment that never resolves client IP addresses does not write one warning per request. In production, treat that warning as a signal to review forwarded-header configuration, proxy trust settings, and middleware ordering.

## Endpoint-Specific Policies

Named policies can be applied to specific endpoints when stricter or specialized protection is needed.

Minimal API example:

```csharp
app.MapGet("/api/data", () => "Limited endpoint")
    .RequireRateLimiting("fixed");
```

Concurrency-sensitive endpoint example:

```csharp
app.MapPost("/admin/export", () => "Export started")
    .RequireRateLimiting("concurrency");
```

Controller or Razor Page handlers can also use rate limiting attributes:

```csharp
using Microsoft.AspNetCore.RateLimiting;

[EnableRateLimiting("fixed")]
public class ReportsController : Controller
{
}
```

## Production Tuning Guidance

Before production use, review the configured limits against expected traffic and endpoint cost.

Consider:

- Anonymous versus authenticated traffic volume.
- Reverse proxy and load balancer behavior.
- Whether client IP, user identity, tenant ID, or API key should define the partition.
- Login, registration, export, report, file upload, and external-service endpoints that may need stricter limits.
- Queue behavior. The template defaults to `QueueLimit: 0` so rejected requests fail quickly instead of building server-side backlog.
- Whether upstream infrastructure such as a CDN, ingress controller, API gateway, or web application firewall already applies additional limits.

If a policy needs authenticated user, tenant, role, or permission data, review middleware ordering. The template currently places rate limiting before authentication so default protections can reject requests before authentication work is performed.

## Middleware Order

Forwarded headers must run before middleware that depends on client IP. The application pipeline applies forwarded headers early and rate limiting after routing:

```csharp
app.UseApplicationForwardedHeaders();
app.UseApplicationRequestLogging();

// ...

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
```

This ordering lets request logging and rate limiting see the corrected `RemoteIpAddress` when trusted proxy configuration is correct.

If a future policy depends on the authenticated user identity, rate limiting may need to move after authentication so user-specific partitioning can be applied.

## Automated Test Strategy

Rate limiting behavior is covered by integration tests under `tests/ProjectTemplate.Web.Tests`.

The tests use `WebApplicationFactory<Program>` to boot the real `ProjectTemplate.Web` pipeline in memory, override rate limiting configuration with in-memory settings, and register a test-only MVC controller from the test assembly.

This keeps production endpoints unchanged while allowing the tests to verify:

- Global fixed-window limiter behavior.
- Named fixed-window policy behavior.
- Named concurrency policy behavior.
- Problem Details and plain-text `429 Too Many Requests` rejection responses.
- Rejection log privacy for the remote IP address.
- Disabled rate limiting behavior.
- Configuration binding for application rate limiting options.
- Client IP partition fallback behavior when `RemoteIpAddress` is unavailable.

See [Runtime Readiness Baseline](runtime-readiness.md) for the consolidated release-readiness view.

## Contract References

[`RateLimitingTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/RateLimitingTests.cs) covers the generated global limiter, named policies, rejection shape, disable path, and unknown-client partition behavior. Forwarded-header tests and `PipelineExtensions.cs` establish the corrected client-IP boundary.

## Learn the Pattern

For broader secure-default and trust-boundary reasoning, see [Secure-by-Default ASP.NET Core Configuration](https://asibackbone.github.io/Learning/aspnetcore/secure-by-default-configuration.html) and [Trust Boundaries and Least Privilege](https://asibackbone.github.io/Learning/security/trust-boundaries-and-least-privilege.html).
