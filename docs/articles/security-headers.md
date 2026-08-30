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

This contract applies when `ProjectTemplate:SecurityHeaders:Enabled` is `true` and the request path does not match `ExcludedPathPrefixes`.

| Header | Default | Contract | Configuration |
|:---|:---|:---|:---|
| `X-Content-Type-Options` | `nosniff` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `X-Frame-Options` | `DENY` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `X-Permitted-Cross-Domain-Policies` | `none` | Required when security headers are enabled and the request path is not excluded | Not individually configurable |
| `Cross-Origin-Opener-Policy` | `same-origin` | Configurable group | Controlled by `EnableCrossOriginHeaders` |
| `Cross-Origin-Resource-Policy` | `same-origin` | Configurable group | Controlled by `EnableCrossOriginHeaders` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=(), usb=(), fullscreen=(self)` | Configurable | Controlled by `EnablePermissionsPolicy` and `PermissionsPolicy` |
| `Content-Security-Policy` | `default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';` | Configurable | Controlled by `EnableContentSecurityPolicy` and `ContentSecurityPolicy` |
| `X-XSS-Protection` | Not emitted | Intentionally omitted | Not supported |

The middleware intentionally does not add `X-XSS-Protection` because that header is obsolete and can create inconsistent behavior in modern browsers.

## Intentional Opt-Outs

The following settings reduce or remove default browser hardening and should be used intentionally:

| Setting | Effect | Recommended use |
|:---|:---|:---|
| `Enabled = false` | Disables all application security headers | Only when an upstream reverse proxy, gateway, or host platform applies equivalent headers |
| `EnableContentSecurityPolicy = false` | Removes CSP | Temporary troubleshooting or applications that must define CSP elsewhere |
| `EnablePermissionsPolicy = false` | Removes Permissions-Policy | Only when browser feature policy is managed elsewhere |
| `EnableCrossOriginHeaders = false` | Removes COOP and CORP | Applications that intentionally integrate cross-origin windows or resources |
| `ExcludedPathPrefixes` | Skips all security headers for matching paths | Infrastructure endpoints such as `/health` and `/metrics` |

## Configuration

Security headers can be configured from `appsettings.json`:
```json
"ProjectTemplate": {
  "SecurityHeaders": {
    "Enabled": true,
    "EnableContentSecurityPolicy": true,
    "EnablePermissionsPolicy": true,
    "EnableCrossOriginHeaders": true,
    "ContentSecurityPolicy": "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';",
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
|`ExcludedPathPrefixes`|Skips security header application for matching request path prefixes.|

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
Content-Security-Policy: default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline';
```
The exact CSP and Permissions-Policy values may differ if overridden by configuration.

## Contract References

[`SecurityHeadersTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/SecurityHeadersTests.cs) verifies enabled/disabled behavior, configured headers, and excluded paths. Pipeline placement is authoritative in `PipelineExtensions.cs`.

## Learn the Pattern

For broader secure-default and trust-boundary reasoning, see [Secure-by-Default ASP.NET Core Configuration](https://asibackbone.github.io/Learning/aspnetcore/secure-by-default-configuration.html) and [Trust Boundaries and Least Privilege](https://asibackbone.github.io/Learning/security/trust-boundaries-and-least-privilege.html).
