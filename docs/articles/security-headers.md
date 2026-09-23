# Security Headers

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application includes configurable security header middleware that applies common HTTP response headers to help reduce browser-based attack surface. The middleware is registered through the application extension pattern so `Program.cs` can remain clean and minimal.

Security headers are registered during service configuration:

```csharp
builder.Services.AddApplicationSecurityHeaders(builder.Configuration);
```
They are applied through the standard application pipeline:
```csharp
app.UseApplicationPipeline();
```
The pipeline calls:
```csharp
app.UseApplicationSecurityHeaders();
```

## Implementation Locations

- Registration/validation: [`SecurityHeadersExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/SecurityHeadersExtensions.cs)
- Header emission: [`SecurityHeadersMiddleware.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Middleware/SecurityHeadersMiddleware.cs)
- Option model/defaults: [`ApplicationSecurityHeadersOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Options/ApplicationSecurityHeadersOptions.cs), [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)

## v1.0 Security Header Contract

This contract applies when `ProjectTemplate:SecurityHeaders:Enabled` is `true` and the request path does not match `ExcludedPathPrefixes`. Responses on excluded paths still receive `X-Content-Type-Options: nosniff` and no other header from this middleware. `Strict-Transport-Security` is registered separately (see [HSTS and Transport Security](#hsts-and-transport-security)) and also applies to excluded paths on HTTPS requests outside Development.

| Header | Default | Contract | Configuration |
|:---|:---|:---|:---|
| `X-Content-Type-Options` | `nosniff` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `X-Frame-Options` | `DENY` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `X-Permitted-Cross-Domain-Policies` | `none` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `Cross-Origin-Opener-Policy` | `same-origin` | Configurable group | Controlled by `EnableCrossOriginHeaders` |
| `Cross-Origin-Resource-Policy` | `same-origin` | Configurable group | Controlled by `EnableCrossOriginHeaders` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)` | Configurable | Controlled by `EnablePermissionsPolicy` and `PermissionsPolicy` |
| `Content-Security-Policy` | `default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self';` | Configurable | Controlled by `EnableContentSecurityPolicy` and `ContentSecurityPolicy` |
| `X-XSS-Protection` | Not emitted | Intentionally omitted | Not supported |
| `Strict-Transport-Security` | Not emitted by this middleware | Emitted by `UseApplicationHsts()` outside Development | See [HSTS and Transport Security](#hsts-and-transport-security)         |

The middleware intentionally does not add `X-XSS-Protection` because that header is obsolete and can create inconsistent behavior in modern browsers.

## HSTS and Transport Security

`Strict-Transport-Security` is intentionally not emitted by this middleware.

HSTS is a transport-layer commitment, not a response-shaping concern. It instructs
a browser to refuse plain HTTP to an origin for the lifetime of `max-age`, and a
misconfigured value cannot be withdrawn by redeploying the application — the
browser honors the cached directive until it expires. That decision belongs to
whoever owns the certificate, the origin, and the rollback path, which in most
deployments is the reverse proxy, ingress controller, CDN, or host platform
rather than the application process.

NCAT therefore emits headers that are safe to apply per-response from this
middleware, registers HSTS separately with framework defaults, and leaves HSTS
values to an explicit deployment decision.

### Where HSTS belongs

ASP.NET Core provides `UseHsts()` and `AddHsts(...)` for application-emitted HSTS.
The generated pipeline calls `UseHsts()` outside Development through
`UseApplicationHsts()` (defined in `SecurityHeadersExtensions`, registered by
`UseProblemDetails()` between the exception handler and status-code pages), with the ASP.NET Core
defaults. A consuming application that leaves HSTS to the edge may remove that
call. Emitting it from both layers is not an error, but only one layer should
own the values.

| Layer                                | When it is the right owner                                                                                      |
| ------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| Reverse proxy, ingress, CDN, gateway | TLS terminates at the edge; the edge serves every host on the origin; rollback is an edge configuration change.  |
| Application (`UseHsts()`)            | The application terminates TLS itself, or the edge cannot be configured and the application owns the public origin. |

If TLS terminates at the edge, the application does not observe the public scheme
without correct forwarded-header trust. See
[Forwarded Headers](forwarded-headers.md) before enabling application-emitted HSTS.

### The decision to make

Whichever layer owns HSTS, the following are explicit choices, not defaults:

- **`max-age`** — start short (for example `300`) and confirm the origin serves
  HTTPS correctly on every path before raising it. Production values are commonly
  `31536000` (one year). A long `max-age` shipped before the origin is ready is
  not reversible from the server side.
- **`includeSubDomains`** — this covers every present and future subdomain of the
  origin, including internal, legacy, or non-HTTPS hosts. Inventory subdomains
  before enabling it.
- **`preload`** — submission to the browser preload list is effectively permanent
  on a multi-month timescale and removal is slow. Treat it as a separate,
  later decision made only after `max-age` and `includeSubDomains` have been
  stable in production.
- **Redirect behavior** — HSTS does not replace an HTTP-to-HTTPS redirect for the
  first request from a browser that has never seen the origin. Confirm the
  redirect exists at the layer that receives plain HTTP.
- **Local development** — ASP.NET Core's `UseHsts()` excludes `localhost` by
  default. Do not add development hosts to an HSTS policy; a cached directive on
  a developer machine outlives the branch that caused it.

Apart from registering `UseHsts()` with framework defaults, NCAT does not
configure, validate, or test HSTS values. An application that adopts HSTS owns
its values, its rollout, and its rollback.

## Intentional Opt-Outs

The following settings reduce or remove default browser hardening and should be used intentionally:

| Setting | Effect | Recommended use |
|:---|:---|:---|
| `Enabled = false` | Disables all application security headers | Only when an upstream reverse proxy, gateway, or host platform applies equivalent headers |
| `EnableContentSecurityPolicy = false` | Removes CSP | Temporary troubleshooting or applications that must define CSP elsewhere |
| `EnablePermissionsPolicy = false` | Removes Permissions-Policy | Only when browser feature policy is managed elsewhere |
| `EnableCrossOriginHeaders = false` | Removes COOP and CORP | Applications that intentionally integrate cross-origin windows or resources |
| `ExcludedPathPrefixes` | Skips every security header except `X-Content-Type-Options` for matching paths | Infrastructure endpoints such as `/health` and `/metrics` |

## Configuration

Security headers can be configured from `appsettings.json`:
```json
"ProjectTemplate": {
  "SecurityHeaders": {
    "Enabled": true,
    "EnableContentSecurityPolicy": true,
    "EnablePermissionsPolicy": true,
    "EnableCrossOriginHeaders": true,
    "ContentSecurityPolicy": "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self';",
    "PermissionsPolicy": "camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)",
    "ExcludedPathPrefixes": [
      "/health",
      "/metrics"
    ]
  }
}
```

## Configuration Options

|Option|Purpose|
|:-----|:------|
|`Enabled`|Enables or disables the security header middleware.|
|`EnableContentSecurityPolicy`|Controls whether the `Content-Security-Policy` header is applied.|
|`EnablePermissionsPolicy`|Controls whether the `Permissions-Policy` header is applied.|
|`EnableCrossOriginHeaders`|Controls whether `Cross-Origin-Opener-Policy` and `Cross-Origin-Resource-Policy` are applied.|
|`ContentSecurityPolicy`|Defines the application Content Security Policy value.|
|`PermissionsPolicy`|Defines the Permissions Policy value.|
|`ExcludedPathPrefixes`|Skips security header application, except `X-Content-Type-Options: nosniff`, for matching request path prefixes. A configured list replaces the code defaults; see the note below.|

A configured `ExcludedPathPrefixes` list replaces the built-in defaults instead of being appended to them, so configuration can both add and remove exclusions. Configuration sources merge arrays by index, so a later source can remove an inherited entry by setting that index to an empty string. Blank entries are ignored. For example, this `appsettings.Production.json` fragment keeps `/health` excluded and applies the full security header set to `/metrics` again:

```json
"ProjectTemplate": {
  "SecurityHeaders": {
    "ExcludedPathPrefixes": [
      "/health",
      ""
    ]
  }
}
```

The same rules apply to `ProjectTemplate:RequestLogging:ExcludedPathPrefixes`.

## Environment-Specific Behavior

The default configuration is intentionally conservative. Applications created from this template can loosen or override headers in environment-specific settings files such as `appsettings.Development.json`.

For example, a local development configuration may temporarily disable CSP while troubleshooting script or style loading:
```json
"ProjectTemplate": {
  "SecurityHeaders": {
    "EnableContentSecurityPolicy": false
  }
}
```
Production applications should use the strongest policy possible for the deployed application. In particular, production deployments should avoid broad CSP allowances such as `unsafe-inline` where practical and should only allow trusted script, style, image, frame, and connection sources.

## Excluded Paths

The default excluded paths are:
```json
[
  "/health",
  "/metrics"
]
```
These paths are commonly used by infrastructure, monitoring tools, or container orchestration systems. Additional paths can be excluded if needed.

## Testing Response Headers

Run the application and inspect the response headers from the root endpoint:
```bash
curl -k -I https://localhost:5001/
```
Expected headers include:
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
X-Permitted-Cross-Domain-Policies: none
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Resource-Policy: same-origin
Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)
Content-Security-Policy: default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self';
```
The exact CSP and Permissions-Policy values may differ if overridden by configuration.

## Contract References

[`SecurityHeadersTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/SecurityHeadersTests.cs) verifies enabled/disabled behavior, configured headers, and excluded paths. Pipeline placement is authoritative in `PipelineExtensions.cs`.

## Learn the Pattern

For broader secure-default and trust-boundary reasoning, see [Secure-by-Default ASP.NET Core Configuration](https://asibackbone.github.io/Learning/aspnetcore/secure-by-default-configuration.html) and [Trust Boundaries and Least Privilege](https://asibackbone.github.io/Learning/security/trust-boundaries-and-least-privilege.html).
