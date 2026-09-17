# Deployment Notes

This article provides initial deployment guidance for applications created from the .NET Core Application Template.

The template includes production-oriented defaults for middleware ordering, forwarded headers, security headers, rate limiting, health checks, structured logging, error handling, authentication foundations, and EF Core data access. These defaults are intended to provide a safe baseline, but every deployment environment should still be reviewed before production use.

See [ADR-0003: Record Release Surface and Distribution Strategy](../adr/0003-record-release-surface-and-distribution-strategy.md) for the release-surface decision.

## Deployment Review Areas

Before deploying an application created from this template, review the following areas:

- Reverse proxy, load balancer, or ingress behavior.
- Forwarded header trust boundaries.
- Environment-specific configuration.
- Secrets and connection string storage.
- HTTPS enforcement and certificate termination.
- Content Security Policy values.
- Rate limit values and queue behavior.
- Health check exposure.
- Database migration execution strategy.
- Logging destinations and retention.

## Reverse Proxy Deployments

Applications are commonly deployed behind a reverse proxy, load balancer, ingress controller, gateway, or hosted platform. In those environments, Kestrel may see the proxy as the immediate client unless forwarded headers are configured correctly.

Review the hosting path for:

- TLS termination location.
- Original client IP preservation.
- Host header forwarding.
- Scheme forwarding from HTTPS to the application.
- Health check probe paths and methods.
- Request size limits.
- Timeout behavior between the proxy and application.

The application should not blindly trust forwarding headers from arbitrary clients. Forwarded headers should only be trusted from known proxies or known networks.

## Forwarded Headers

Forwarded headers are configured under `ProjectTemplate:ForwardedHeaders`.

Production deployments should explicitly configure trusted proxy addresses or networks:

```json
"ProjectTemplate": {
  "ForwardedHeaders": {
    "Enabled": true,
    "RequireExplicitProxyTrust": true,
    "ForwardLimit": 1,
    "ClearKnownNetworksAndProxies": true,
    "KnownProxies": [
      "10.0.0.10"
    ],
    "KnownNetworks": [
      "10.0.0.0/24"
    ],
    "AllowedHosts": [
      "example.com"
    ]
  }
}
```

Recommended production posture:

- Enable forwarded headers only when the application is actually behind trusted infrastructure.
- Keep `RequireExplicitProxyTrust` enabled outside Development so missing trust configuration fails before the application serves traffic.
- Keep `ForwardLimit` as low as the deployment topology allows.
- Prefer explicit `KnownProxies` or `KnownNetworks` values.
- Avoid clearing known proxy and network restrictions unless the hosting environment requires it and another trusted boundary is present.
- Only enable host forwarding when required.
- Configure `AllowedHosts` when host forwarding is enabled.

The Kubernetes manifest includes `10.244.0.0/16` only as an example ingress proxy network. Replace it with the narrow CIDR actually used by the target cluster before deployment; an incorrect range either leaves forwarded client addresses untrusted or trusts more senders than intended.

See [Forwarded Headers and Proxy Support](forwarded-headers.md) for the detailed forwarded header configuration model.

## Environment-Specific Configuration

Use environment-specific configuration to separate local development settings from production settings.

Common layers include:

- `appsettings.json` for shared defaults.
- `appsettings.Development.json` for local development overrides.
- Environment variables for deployment-specific values.
- User secrets for local-only developer secrets.
- Hosting platform secret stores for production secrets.

ASP.NET Core configuration uses a layered model where later providers can override earlier providers. Production deployments should avoid storing production secrets directly in committed JSON files.

Example environment variable names can use double underscores to represent nested configuration keys:

```powershell
$env:ProjectTemplate__RateLimiting__GlobalFixedWindow__PermitLimit = "120"
$env:ProjectTemplate__SecurityHeaders__EnableContentSecurityPolicy = "true"
$env:ConnectionStrings__DefaultConnection = "Server=...;Database=...;Encrypt=True;TrustServerCertificate=False;"
```

## Secrets and Connection Strings

Do not commit production secrets, production connection strings, certificates, tokens, client secrets, or signing keys to the repository.

Recommended production posture:

- Store production secrets in the hosting platform secret manager or an approved external secret store.
- Use encrypted connection strings where supported.
- Use least-privilege database accounts.
- Rotate secrets when access changes.
- Keep development connection strings separate from production connection strings.
- Prefer environment-specific overrides over editing committed base configuration.

Local development may use user secrets for sensitive development-only values:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=projecttemplate.db" --project src/ProjectTemplate.Web
```

## HTTPS and Certificate Termination

Production deployments should serve public traffic over HTTPS.

When TLS terminates at a reverse proxy or load balancer, confirm that:

- The proxy forwards the original scheme to the application.
- Forwarded headers are trusted only from the proxy.
- Redirect behavior does not create loops.
- Public endpoints generate HTTPS URLs where applicable.
- Authentication callback URLs match the public HTTPS origin.

When TLS terminates directly in Kestrel, configure certificates through the hosting environment or approved certificate management process.

## Data Protection Key Ring

ASP.NET Core Data Protection secures authentication cookies, antiforgery tokens, and other protected application payloads. The template persists its key ring using `ProjectTemplate:DataProtection`:

```json
"ProjectTemplate": {
  "DataProtection": {
    "ApplicationName": "ProjectTemplate.Web",
    "KeyRingPath": "/app/data-protection-keys"
  }
}
```

`ApplicationName` is the isolation boundary for protected payloads. Every replica of the same application must use the same value and the same key-ring storage. `KeyRingPath` may be absolute or relative to the application content root.

For containers and orchestrated deployments:

- Mount the key-ring path on durable storage that survives container and pod replacement.
- Share the same key ring across all replicas. The Kubernetes example uses a `ReadWriteMany` persistent volume claim; select a storage class that supports multi-writer access, or replace filesystem persistence with an approved shared Data Protection provider.
- Restrict read and write access to the application identity. Key-ring files are security-sensitive and should be encrypted at rest with a key-encryption certificate, an organization-approved key management system, or a storage control.
- Back up and retain active keys for at least as long as protected payloads may remain valid. Deleting the key ring or changing `ApplicationName` invalidates existing authentication cookies and antiforgery tokens.

### Key-Ring Encryption at Rest

Without application-level encryption, key-ring files are written in plain text on Linux and macOS. On Windows the framework protects them with DPAPI for the current user, which prevents sharing the key ring across machines.

To encrypt key-ring files with a certificate, supply a PKCS#12 (`.pfx`) certificate that includes its private key:

```json
"ProjectTemplate": {
  "DataProtection": {
    "ApplicationName": "ProjectTemplate.Web",
    "KeyRingPath": "/app/data-protection-keys",
    "KeyEncryptionCertificatePath": "/run/secrets/data-protection.pfx"
  }
}
```

Supply `KeyEncryptionCertificatePassword` from a secret store, environment variable, or mounted secret rather than `appsettings.json`:

```bash
ProjectTemplate__DataProtection__KeyEncryptionCertificatePassword=<from secret store>
```

Behavior and constraints:

- New keys are encrypted with the certificate, and the same certificate is used to decrypt them, so decryption works on Linux and macOS without a certificate store.
- Startup fails when the certificate file is missing, has no private key, or when a password is configured without a certificate path.
- Every replica must load the same certificate. Keys already written in plain text remain readable and are replaced as the key ring rotates.
- Retain an expiring certificate until every key it encrypted has aged out of the key ring. Rotating to a new certificate while old keys are still active requires also making the previous certificate available for decryption, which this template does not configure; plan certificate rotation alongside key lifetime.

### Startup Posture Warnings

Outside Development, the application writes startup warnings for Data Protection postures that commonly fail in deployment:

| Event ID | Condition |
|:---|:---|
| `1003` | `KeyRingPath` is relative, so the key ring is stored under the content root and is usually replaced with the application in containers. |
| `1004` | `KeyEncryptionCertificatePath` is not set, so key-ring files are not encrypted by the application. |

These warnings are informational. Suppress them only after confirming durable shared storage and storage-level encryption are in place.

The Docker Compose example mounts `/app/data-protection-keys` from a named volume. This provides restart persistence for local Compose deployments; production replicas require genuinely shared durable storage rather than one volume per host.

## Production Content Security Policy Review

The template includes configurable security headers and a conservative baseline Content Security Policy. Production applications should review the CSP before release.

Review these directives carefully:

- `default-src`
- `script-src`
- `style-src`
- `img-src`
- `connect-src`
- `frame-ancestors`
- `form-action`
- `base-uri`

Recommended production posture:

- Allow only trusted script, style, image, frame, and connection sources.
- Avoid broad wildcard sources where practical.
- Avoid `unsafe-inline` for scripts in production when the application can support nonce, hash, or bundled script approaches.
- Keep `frame-ancestors 'none'` unless the application is intentionally embedded.
- Test authentication, documentation pages, static assets, and external provider redirects after CSP changes.

See [Security Headers](security-headers.md) for the detailed security header configuration model.

## Rate Limit Tuning

The template includes baseline global and named rate limiting policies. Default limits are intentionally conservative and should be reviewed before production use.

Review rate limits against expected traffic:

- Normal browser traffic.
- API client traffic.
- Authentication redirects and callbacks.
- Health checks and monitoring probes.
- Static file and documentation traffic.
- Administrative or resource-heavy endpoints.

Recommended production posture:

- Tune permit limits based on measured traffic rather than guesswork.
- Keep queue limits low for public-facing endpoints unless the user experience requires queuing.
- Use concurrency limits for expensive or contention-prone operations.
- Monitor `429 Too Many Requests` responses after deployment.
- Consider stricter policies for authentication, export, reporting, administrative, or integration endpoints.
- Revisit limits after real traffic patterns are known.

See [Rate Limiting](rate-limiting.md) for the detailed rate limiting configuration model.

## Health Checks

Health checks are useful for load balancers, uptime monitors, and container orchestration platforms. Production deployments should verify that health check endpoints are exposed only as broadly as needed.

Recommended production posture:

- Keep simple liveness probes safe for infrastructure access.
- Avoid exposing sensitive dependency details publicly.
- Use infrastructure rules to restrict detailed health information if needed.
- Confirm probe method, path, and expected status code with the hosting platform.

## Database Migrations

The template documents EF Core migration execution guidance separately. Production deployments should avoid applying migrations automatically on application startup unless that behavior is explicitly accepted for the target environment.

Recommended production posture:

- Run migrations as an explicit deployment step.
- Review generated SQL before production execution when required.
- Back up production databases before schema changes.
- Keep migration execution separate from normal web request startup.

See [Data Access](data-access.md) for migration and provider guidance.

## Deployment Validation Checklist

Use this checklist before production release:

```text
[ ] Confirm hosting environment and TLS termination path.
[ ] Confirm forwarded header settings match the reverse proxy topology.
[ ] Confirm trusted proxies or networks are configured.
[ ] Confirm production secrets are not stored in committed files.
[ ] Confirm production connection strings come from environment or secret storage.
[ ] Confirm HTTPS redirects and callback URLs work externally.
[ ] Confirm the Data Protection key ring is durable, access-restricted, and shared by all replicas using the same application name.
[ ] Confirm Data Protection key-ring files are encrypted at rest by a key-encryption certificate or storage-level control, and that startup warnings 1003 and 1004 are resolved or accepted.
[ ] Confirm Content Security Policy works with deployed assets and auth flows.
[ ] Confirm rate limits match expected traffic and monitoring behavior.
[ ] Confirm health checks are reachable by infrastructure but do not leak sensitive details.
[ ] Confirm database migration execution strategy.
[ ] Confirm logs are written to the expected production destination.
[ ] Confirm error responses do not expose sensitive implementation details.
```

Deployment readiness is environment-specific. Treat this article as a starting checklist, not a replacement for organization-specific production review.
