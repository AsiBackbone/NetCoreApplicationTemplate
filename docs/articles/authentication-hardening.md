# Production Authentication Hardening Checklist

The template enables local cookie authentication in the default scaffold and provides starter configuration for optional external providers. Production authentication remains provider-specific and environment-specific.

Authentication establishes identity. Authorization determines whether that identity may access an endpoint or operation. The default scaffold also requires authentication for unannotated routed endpoints through the fallback authorization policy, but neither that policy nor starter provider configuration completes a production security model.

Treat this checklist as required review before deploying any generated application that enables cookie authentication, OpenID Connect, SAML2, Microsoft, Google, GitHub, or another provider.

## Baseline Principle

The template provides concrete baseline behavior: cookie configuration, provider registration seams, startup validation, claims transformation, authenticated fallback access, and explicit anonymous endpoint exceptions. The consuming application still owns real redirect URIs, provider trust boundaries, claim semantics, MFA policy, session policy, and protected configuration.

Before deployment, confirm:

- Every enabled provider has production-owned settings.
- Placeholder development values have been replaced.
- Environment-specific URLs match externally visible host names.
- Claims are translated into application-owned meanings before they drive authorization.
- Login, logout, session timeout, access-denied, and MFA expectations are tested.
- The anonymous endpoint allowlist and fallback authorization posture match the application design.

## HTTPS, Reverse Proxy, and Forwarded Headers

- Confirm production traffic uses HTTPS through the intended trust boundary.
- Configure forwarded headers only for trusted proxy networks or known proxies.
- Confirm the application sees the correct external scheme, host, and path base.
- Confirm login, callback, logout, and access-denied flows use the public origin.
- Test security headers, HTTPS redirection, and callbacks behind the deployed proxy.

## Authentication Cookie Secure Policy

The local authentication cookie uses `CookieSecurePolicy.Always` by default. The cookie therefore receives the `Secure` attribute independently of the request scheme perceived by the application. Production deployments must serve authentication flows over HTTPS; a reverse-proxy or forwarded-header misconfiguration does not downgrade this cookie to a non-secure cookie.

Local plain-HTTP authentication is available only through an explicit Development-only override:

```json
{
  "ProjectTemplate": {
    "Authentication": {
      "Cookie": {
        "AllowInsecureHttp": true
      }
    }
  }
}
```

Place this override only in local `appsettings.Development.json`, user secrets, or another Development-only configuration source. When enabled in Development, the cookie uses `CookieSecurePolicy.SameAsRequest`, so an HTTP request can receive the cookie without the `Secure` attribute.

Startup validation rejects `ProjectTemplate:Authentication:Cookie:AllowInsecureHttp=true` in Testing, Staging, Production, or any environment other than Development. Do not use the override to compensate for TLS, proxy, forwarded-header, certificate, or callback configuration problems.

## Redirect, Callback, ACS, and Logout URLs

- Register OIDC and OAuth redirect URIs exactly.
- Match SAML assertion consumer service URLs to deployed metadata.
- Separate local development callbacks from production callbacks.
- Avoid wildcard redirect registrations unless explicitly required and reviewed.
- Keep return URLs local or otherwise strictly validated.
- Review provider metadata, entity IDs, callback paths, and module paths per environment.

## Cookie Security

- Require secure transport for authentication cookies in production.
- Keep cookies `HttpOnly` unless a different design explicitly requires otherwise.
- Confirm `SameSite` behavior works with the selected provider flows.
- Avoid cookie-name, domain, and path collisions across applications.
- Document sliding expiration, absolute expiration, and session timeout.
- Verify logout clears the intended local cookie.

## Data Protection Key Persistence

Authentication cookies and antiforgery tokens depend on ASP.NET Core Data Protection. Persist the configured key ring on durable, access-restricted storage, and ensure every replica uses the same key ring and `ProjectTemplate:DataProtection:ApplicationName`. A container-local or pod-local key ring causes active sessions to fail after replacement and allows replicas to reject one another's cookies.

Treat key-ring files as security-sensitive material. Protect them at rest using organization-approved storage or key management controls, retain them for the lifetime of protected payloads, and test sign-in plus antiforgery flows across replica boundaries and rolling restarts. See [Deployment Notes](deployment.md#data-protection-key-ring) for configuration and volume guidance.

## Claims Translation and Authorization

Do not wire raw external claims directly into application authorization without a translation strategy.

- Identify a stable subject identifier.
- Normalize provider-specific claims into application-owned claim names.
- Map external groups to application roles deliberately.
- Define conflict behavior when multiple providers can authenticate the same user.
- Add tests for every transformation that affects named authorization policies.
- Test missing-role and missing-permission cases as denied access.

The fallback authorization policy establishes an authenticated-user floor. Named role, permission, claim, and custom policies must still be applied where stronger authorization is required.

## Provider Credentials and Tokens

- Keep provider credentials and signing material outside committed configuration.
- Confirm values can be rotated without rebuilding the application.
- Retain tokens only when the application has a documented need and protection strategy.
- Align token, cookie, refresh, and session lifetimes.
- Confirm logs exclude authorization codes, assertions, cookies, and provider credentials.
- Keep certificate and issuer validation enabled in production.

## Login, Logout, Session, and MFA Behavior

- Confirm login starts the expected provider challenge.
- Confirm callback handling succeeds through the deployed public URL.
- Confirm explicit anonymous login and failure endpoints do not create redirect loops.
- Confirm `POST /Account/Logout` requires an authenticated user and anti-forgery validation.
- Confirm session expiration requires reauthentication.
- Define behavior for disabled users and changed roles or groups.
- Confirm provider-owned MFA or step-up behavior for high-risk operations.

## Minimum Smoke-Test Matrix

| Scenario | Expected result |
|---|---|
| Anonymous request to protected browser route | Redirects through the configured challenge scheme. |
| Anonymous request to protected API route | Returns the configured API authentication challenge response. |
| Explicit anonymous route | Remains reachable without authentication. |
| Successful login | Returns with the expected local session. |
| Invalid callback or unknown provider | Fails safely. |
| External or malformed return URL | Is rejected or handled safely. |
| Claims transformation | Produces the required application-owned claims. |
| Missing required authorization claim | Is denied. |
| Logout | Requires authentication and anti-forgery validation and clears local session state. |
| Reverse-proxy deployment | Uses the public HTTPS origin for redirects and callbacks. |

## Release Review Sign-Off

Record the following decisions in the consuming application's deployment notes:

- Enabled providers and production registration identifiers.
- Approved redirect, callback, ACS, and logout URLs.
- Cookie lifetime and session policy.
- Claims translation and authorization policy contract.
- Explicit anonymous endpoint allowlist.
- Fallback authorization setting and any application-wide opt-out.
- Provider metadata validation and MFA ownership.
- Smoke-test results.

A generated application can be production-oriented without being production-complete. Production readiness requires binding the baseline to the consuming application's real identity provider, deployment, claims, session, authorization, and operational policies.
