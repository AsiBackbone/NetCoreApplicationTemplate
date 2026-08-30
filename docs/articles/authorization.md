# Authorization

NCAT registers ASP.NET Core authorization with named policies and, by default, an authenticated-user fallback policy. `DefaultPolicy` handles endpoints that explicitly request authorization without a named policy; `FallbackPolicy` protects routed endpoints with no authorization metadata. Explicit anonymous metadata bypasses the fallback policy.

## Role and Permission Authorization Policies

The application includes named policy patterns for authenticated users, role-based access, and permission-based access.

| Policy | Purpose |
|---|---|
| `application.AuthenticatedUser` | Requires an authenticated user. |
| `application.Role.Administrator` | Requires a normalized administrator role claim. |
| `application.Permission.ManageApplication` | Requires a normalized manage-application permission claim. |

Default normalized claim types:

| Claim type | Purpose |
|---|---|
| `application:role` | Role claim used by role-based policies. |
| `application:permission` | Permission claim used by permission-based policies. |

Configuration example:

```json
"ProjectTemplate": {
  "Authorization": {
    "RequireAuthenticatedUserByDefault": true,
    "RoleClaimType": "application:role",
    "PermissionClaimType": "application:permission",
    "AdministratorRoles": [
      "Administrator"
    ],
    "ManageApplicationPermissions": [
      "application.manage"
    ]
  }
}
```

## Default Authorization Posture

The default scaffold is **closed by default for routed endpoints**. When `ProjectTemplate:Authorization:RequireAuthenticatedUserByDefault` is `true`, the application configures a fallback authorization policy that requires an authenticated user for endpoints without authorization metadata.

This protects newly added controller actions, Razor Pages, and other routed endpoints when a developer does not attach `[Authorize]` or a named policy. Stronger role, permission, claim, or custom policies still apply where declared explicitly.

Intentionally public endpoints must use explicit anonymous metadata. Public access is an exception to the fallback policy, not the absence of a security decision.

The `--authProvider none` scaffold is an explicit opt-out. It sets application authentication, cookie authentication, and the authenticated fallback requirement to `false`. Unannotated routed endpoints are public until the consuming application adds another authentication mechanism and authorization posture. Startup validation rejects the inconsistent combination of disabled authentication and an enabled authenticated fallback policy.

### Deliberate application-wide opt-out

A consuming application that intentionally wants unannotated routed endpoints to remain public can disable the fallback policy:

```json
"ProjectTemplate": {
  "Authorization": {
    "RequireAuthenticatedUserByDefault": false
  }
}
```

This is an application-wide security posture change. Prefer explicit anonymous metadata for isolated public routes.

## Endpoint Access Classification

| Endpoint or endpoint group | Classification | Rationale |
|:---|:---|:---|
| `GET /Account/Login` | Explicitly anonymous | Unauthenticated users must be able to enter the authentication flow. |
| `GET /External/Challenge` | Explicitly anonymous | Starts a configured external-provider challenge after validating the provider and return URL. |
| External-provider callback and remote authentication paths | Authentication-middleware owned | OAuth, OpenID Connect, SAML2, and similar handlers process configured callback paths. |
| `GET /Account/AccessDenied` | Explicitly anonymous | Prevents failure handling from creating a redirect loop. |
| `GET /Home/Error/{statusCode?}` | Explicitly anonymous | Error handling must render a terminal response for anonymous requests. |
| `/health`, `/health/live`, `/health/ready` | Explicitly anonymous; deployment exposure is operator-controlled | Infrastructure probes remain independent of browser login state. Production reachability should be restricted at the network or ingress boundary. |
| `POST /Account/Logout` | Explicitly authenticated | Logout requires `[Authorize]` and anti-forgery validation. |
| Starter Razor Page `/` | Authenticated by fallback | Demonstrates the closed-by-default posture. |
| Sample application-information API routes | Authenticated by fallback | Informational APIs are not public implicitly. |
| Future routed endpoints | Authenticated by fallback | New routes remain protected unless an anonymous exception is reviewed and tested. |
| Static files and browser assets | Static-file middleware | `UseStaticFiles` runs before routing. Sensitive files must not be placed under the public web root. |

The fallback-policy contract is exercised by `FallbackAuthorizationPolicyTests`, including explicit anonymous endpoints and unannotated controller/Razor Page behavior.

## Named Policy Usage Examples

Require any authenticated user:

```csharp
[Authorize(Policy = ApplicationAuthorizationPolicyNames.AuthenticatedUser)]
public IActionResult SecurePage()
{
    return View();
}
```

Require an administrator role:

```csharp
[Authorize(Policy = ApplicationAuthorizationPolicyNames.AdministratorRole)]
public IActionResult AdminOnly()
{
    return View();
}
```

Require the manage-application permission:

```csharp
[Authorize(Policy = ApplicationAuthorizationPolicyNames.ManageApplicationPermission)]
public IActionResult ManageApplication()
{
    return View();
}
```

## NCAT and AsiBackbone Responsibility Boundary

NCAT supplies ASP.NET Core authentication, endpoint authorization, middleware ordering, request protection, and application infrastructure. These controls establish identity and determine whether a request may reach an endpoint.

A consuming application may integrate AsiBackbone for application-level policy decisions, acknowledgment workflows, scoped capability grants, and decision audit records around protected operations. AsiBackbone governance complements but does not replace ASP.NET Core authentication or endpoint authorization. An operation should first pass the NCAT endpoint boundary before application-level governance evaluates the requested action.

## Claims Normalization Relationship

Authorization policies are designed to work with the claims transformation layer documented in [Authentication](authentication.md). External providers often emit different role, group, permission, or scope claim names. The template can normalize those claims into application-owned names so authorization policies remain stable across providers.

See [Runtime Readiness Baseline](runtime-readiness.md) for the consolidated release-readiness view.

## Contract References

[`AuthorizationPolicyTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/AuthorizationPolicyTests.cs) verifies named policies. [`FallbackAuthorizationPolicyTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/FallbackAuthorizationPolicyTests.cs) verifies the closed-by-default fallback posture, explicit anonymous access, opt-out behavior, and invalid authentication/authorization combinations. [`ClaimsTransformationTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ClaimsTransformationTests.cs) covers normalized claims.

## Learn the Pattern

See [When ASP.NET Core Authorization Is Enough](https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html) and [Role-Based, Claims-Based, and Capability-Based Authorization](https://asibackbone.github.io/Learning/architecture/role-based-claims-based-and-capability-based-authorization.html).
