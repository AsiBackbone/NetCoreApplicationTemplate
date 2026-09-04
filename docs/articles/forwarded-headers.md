# Forwarded Headers and Proxy Support

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application includes optional forwarded headers support for deployments behind reverse proxies,
load balancers, ingress controllers, and hosted infrastructure.

Forwarded headers allow the application to correctly resolve the original client IP address,
request scheme, and host when traffic is forwarded through another server before reaching Kestrel.

Configuration is controlled through `appsettings.json`:

```json
"ProjectTemplate": {
  "ForwardedHeaders": {
    "Enabled": true,
    "RequireExplicitProxyTrust": true,
    "Headers": [
      "XForwardedFor",
      "XForwardedProto"
    ],
    "ForwardLimit": 1,
    "RequireHeaderSymmetry": false,
    "ClearKnownNetworksAndProxies": false,
    "KnownProxies": [],
    "KnownNetworks": [],
    "AllowedHosts": []
  }
}
```

By default, the application processes:

- `X-Forwarded-For`
- `X-Forwarded-Proto`

Production deployments should explicitly configure trusted proxy IP addresses or trusted proxy
networks using `KnownProxies` or `KnownNetworks`.

Do not trust raw `X-Forwarded-For` values in application code. Forwarded headers are safe to use
only after ASP.NET Core has processed them through trusted proxy configuration. Middleware that
reads `HttpContext.Connection.RemoteIpAddress`, including request logging and client IP rate
limiting, should rely on the corrected `RemoteIpAddress` value rather than parsing forwarded
headers directly.


## Implementation Locations

- Registration/validation/middleware activation: [`ForwardedHeadersExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/ForwardedHeadersExtensions.cs)
- Option model: [`ApplicationForwardedHeadersOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Options/ApplicationForwardedHeadersOptions.cs)
- Production trust diagnostic: [`ForwardedHeadersTrustDiagnosticsHostedService.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Diagnostics/ForwardedHeadersTrustDiagnosticsHostedService.cs)
- Generated defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)

## Startup Trust Diagnostic

Outside the Development environment, the application fails startup by default when all of the
following are true:

- forwarded headers are enabled and include `X-Forwarded-For`;
- application rate limiting is enabled; and
- neither `KnownProxies` nor `KnownNetworks` contains a deployment-specific trust entry.

This fail-fast default prevents `RemoteIpAddress` from silently remaining the ingress address and
placing every downstream client into one rate-limit partition. It also prevents client-IP logs from
silently identifying the proxy rather than the originating client.

Configure a deployment-specific trust boundary before running outside Development:

```json
"ForwardedHeaders": {
  "RequireExplicitProxyTrust": true,
  "KnownProxies": [ "10.0.0.10" ],
  "KnownNetworks": [ "10.0.0.0/24" ]
}
```

`RequireExplicitProxyTrust` is ignored in Development so the template's normal loopback and local
scenarios continue to work. In every other environment, strict mode fails startup when forwarded
client-IP processing and rate limiting are active without a configured trusted proxy or network.
Setting `RequireExplicitProxyTrust` to `false` restores warning-only behavior, but production
deployments should prefer configuring the actual proxy trust boundary or disabling forwarded
headers when no proxy is present.

`UseForwardedHeaders()` must run before middleware that depends on the client IP, request scheme,
host, or path base. The template's centralized pipeline applies forwarded headers before request
logging, HTTPS redirection, routing, CORS, rate limiting, authentication, authorization, and
endpoint execution.

`XForwardedHost` is intentionally not enabled by default. If enabled, configure `AllowedHosts`
to reduce the risk of host header spoofing.

See [Rate Limiting](rate-limiting.md) for client-IP partition behavior and [Deployment](deployment.md)
for production proxy and hosting guidance.

## Contract References

See [`ForwardedHeadersTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ForwardedHeadersTests.cs), [`ForwardedHeadersExtensionsCoverageTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ForwardedHeadersExtensionsCoverageTests.cs), and [`ForwardedHeadersTrustDiagnosticsTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ForwardedHeadersTrustDiagnosticsTests.cs). `PipelineExtensions.cs` establishes that forwarded-header processing runs first.

## Learn the Pattern

For general trust-boundary and secure-configuration reasoning around proxies and caller-controlled metadata, see [Trust Boundaries and Least Privilege](https://asibackbone.github.io/Learning/security/trust-boundaries-and-least-privilege.html) and [Secure-by-Default ASP.NET Core Configuration](https://asibackbone.github.io/Learning/aspnetcore/secure-by-default-configuration.html).
