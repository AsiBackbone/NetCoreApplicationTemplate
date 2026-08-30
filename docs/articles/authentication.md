# Authentication

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.


## Implementation Locations

- Authentication registration/cookie baseline: [`AuthenticationServiceExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Authentication/Extensions/AuthenticationServiceExtensions.cs)
- Options/validation: [`Authentication/Options`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/src/ProjectTemplate.Web/Authentication/Options)
- Provider integrations: [`Authentication/Providers`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/src/ProjectTemplate.Web/Authentication/Providers)
- Login/logout and challenge endpoints: [`AccountController.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Controllers/AccountController.cs), [`ExternalController.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Controllers/ExternalController.cs)
- Generated defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)

## Default Authentication Posture

The base application enables the application authentication module and local cookie authentication by default.

By default:

- `ProjectTemplate:Authentication:Enabled` is `true`.
- The default authenticate, challenge, and sign-in schemes use `Cookies`.
- Local cookie authentication is enabled.
- External providers such as OpenID Connect, SAML2, Microsoft, Google, and GitHub are disabled.

This gives applications a working local authentication baseline while keeping external identity provider integration opt-in.

To enable an external provider, keep application authentication enabled and set only the required provider configuration to enabled. For example, OIDC requires `ProjectTemplate:Authentication:Providers:OpenIdConnect:Enabled` to be set to `true` along with valid authority, client ID, and client secret values.

Before enabling any real provider in production, review the [Production Authentication Hardening Checklist](authentication-hardening.md). Generated provider settings are starter configuration and must be bound to the consuming application's production URLs, provider registrations, claims contract, token policy, secret-management approach, session behavior, and MFA expectations.

### OpenID Connect

The application includes standards-based OpenID Connect authentication support.
External OIDC provider integration is disabled by default. To enable it, configure the `ProjectTemplate:Authentication` section and set both authentication and the OpenID Connect provider to enabled.

```json
"ProjectTemplate": {
  "Authentication": {
    "Enabled": true,
    "DefaultScheme": "Cookies",
    "DefaultChallengeScheme": "OpenIdConnect",
    "DefaultSignInScheme": "Cookies",
    "Providers": {
      "OpenIdConnect": {
        "Enabled": true,
        "Scheme": "OpenIdConnect",
        "DisplayName": "OpenID Connect",
        "Authority": "https://login.example.com",
        "ClientId": "",
        "ClientSecret": "",
        "CallbackPath": "/signin-oidc",
        "ResponseType": "code",
        "SaveTokens": true,
        "Scopes": [
          "openid",
          "profile",
          "email"
        ]
      }
    }
  }
}
```
_Do not commit real client secrets to source control. Use user secrets, environment variables, deployment secrets, or a secure secret store._

## SAML2

The application includes standards-based SAML2 authentication support.
External SAML2 provider integration is disabled by default. To enable it, configure the `ProjectTemplate:Authentication` section and set both authentication and the Saml2 provider to enabled.
```json
"ProjectTemplate": {
  "Authentication": {
    "Enabled": true,
    "DefaultScheme": "Cookies",
    "DefaultChallengeScheme": "Saml2",
    "DefaultSignInScheme": "Cookies",
    "Providers": {
      "Saml2": {
        "Enabled": true,
        "Scheme": "Saml2",
        "DisplayName": "SAML2",
        "EntityId": "https://localhost:5001/saml2",
        "MetadataUrl": "https://idp.example.com/metadata",
        "ModulePath": "/Saml2/Acs",
        "LoadMetadata": true,
        "RequireSignedAssertions": true,
        "ValidateCertificates": true
      }
    }
  }
}
```
_Do not commit real certificates, private keys, or real IdP metadata to source control. Use user secrets, environment variables, deployment secrets, or a secure secret store._

## Microsoft External Provider

The application includes Microsoft external authentication support through `Microsoft.AspNetCore.Authentication.MicrosoftAccount`.

The Microsoft provider is disabled by default and only registers when:

`ProjectTemplate:Authentication:Providers:Microsoft:Enabled`

is set to `true`.

```json
"ProjectTemplate": {
  "Authentication": {
    "Enabled": true,
    "DefaultScheme": "Cookies",
    "DefaultChallengeScheme": "Microsoft",
    "DefaultSignInScheme": "Cookies",
    "Providers": {
      "Microsoft": {
        "Enabled": true,
        "Scheme": "Microsoft",
        "DisplayName": "Microsoft",
        "ClientId": "",
        "ClientSecret": "",
        "CallbackPath": "/signin-microsoft",
        "Scopes": []
      }
    }
  }
}
```
_Do not commit real client IDs, client secrets, certificates, tokens, or provider credentials to source control. Use user secrets, environment variables, deployment secrets, or a secure secret store._

## Google External Provider

The application includes Google external authentication support through `Microsoft.AspNetCore.Authentication.Google`.

The Google provider is disabled by default and only registers when:

`ProjectTemplate:Authentication:Providers:Google:Enabled`

is set to `true`.

```json
"ProjectTemplate": {
  "Authentication": {
    "Enabled": true,
    "DefaultScheme": "Cookies",
    "DefaultChallengeScheme": "Google",
    "DefaultSignInScheme": "Cookies",
    "Providers": {
      "Google": {
        "Enabled": true,
        "Scheme": "Google",
        "DisplayName": "Google",
        "ClientId": "",
        "ClientSecret": "",
        "CallbackPath": "/signin-google",
        "Scopes": [
          "profile",
          "email"
        ]
      }
    }
  }
}
```
_Do not commit real client IDs, client secrets, certificates, tokens, or provider credentials to source control. Use user secrets, environment variables, deployment secrets, or a secure secret store._

## GitHub External Provider
The application includes GitHub external authentication support through `AspNet.Security.OAuth.GitHub`.

The GitHub provider is disabled by default and only registers when:

`ProjectTemplate:Authentication:Providers:GitHub:Enabled`

is set to `true`.

```json
"ProjectTemplate": {
  "Authentication": {
    "Enabled": true,
    "DefaultScheme": "Cookies",
    "DefaultChallengeScheme": "GitHub",
    "DefaultSignInScheme": "Cookies",
    "Providers": {
      "GitHub": {
        "Enabled": true,
        "Scheme": "GitHub",
        "DisplayName": "GitHub",
        "ClientId": "",
        "ClientSecret": "",
        "CallbackPath": "/signin-github",
        "Scopes": [
          "profile",
          "email"
        ]
      }
    }
  }
}
```
_Do not commit real client IDs, client secrets, certificates, tokens, or provider credentials to source control. Use user secrets, environment variables, deployment secrets, or a secure secret store._

## Authentication Provider Startup Validation

Authentication provider configuration is validated during application startup.

Provider-specific values are only required when that provider is enabled. Disabled providers may keep placeholder or empty values so the base application remains safe to run without external identity-provider setup.

When a provider is enabled, startup validation fails fast if required values are missing. Validation messages identify the missing configuration key, but do not log configured secret values.

Validated providers include:

- OpenID Connect
- SAML2
- Microsoft
- Google
- GitHub

This prevents partially configured authentication providers from failing later during runtime login flows.

## Baseline Authentication Endpoints

The application provides minimal account and external authentication endpoints:

| Endpoint | Purpose |
|---|---|
| `GET /Account/Login` | Displays the baseline login page and available registered external providers. |
| `POST /Account/Logout` | Signs out of the local cookie session. Requires anti-forgery validation. |
| `GET /Account/AccessDenied` | Displays a safe access denied response. |
| `GET /External/Challenge` | Starts an external authentication challenge for a registered provider scheme. |

`/External/Challenge` accepts a `provider` value and an optional `returnUrl`.

Return URLs are validated as local URLs before redirecting to avoid open redirect vulnerabilities. Unknown provider schemes are rejected safely. Provider secrets, tokens, cookies, and sensitive query-string values should not be logged.

## Current External Provider Implementation

NCAT currently uses provider-specific ASP.NET Core authentication handlers for Microsoft, Google, and GitHub, plus dedicated OpenID Connect and SAML2 integrations. They register only when enabled and pass the startup-validation boundary above. Replacing them with a different client architecture would be an NCAT implementation change and is outside this current-behavior contract.

## Claims Transformation and Normalization

The application includes an optional claims transformation layer that normalizes provider-specific claims into application-owned claim names.

External identity providers often use different claim names for the same concept. For example, one provider may emit `sub`, another may emit `nameidentifier`, and another may use a SAML claim URI. The claims transformation layer allows these inputs to be mapped into consistent application claim names such as:

- `application:subject`
- `application:name`
- `application:email`
- `application:role`
- `application:group`
- `application:permission`

Original provider claims are preserved by default. They are only removed when `ProjectTemplate:Authentication:ClaimsTransformation:RemoveOriginalClaims` is explicitly set to `true`.

## Contract References

See [`AuthenticationTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/AuthenticationTests.cs), [`AuthenticationProviderIntegrationTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/AuthenticationProviderIntegrationTests.cs), [`AuthenticationProviderOptionCoverageTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/AuthenticationProviderOptionCoverageTests.cs), [`AuthenticationCookieSecurePolicyTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/AuthenticationCookieSecurePolicyTests.cs), [`ExternalAuthenticationEndpointTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ExternalAuthenticationEndpointTests.cs), and [`ClaimsTransformationTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ClaimsTransformationTests.cs).

## Learn the Pattern

For general trust-boundary and secret-handling guidance, see [Trust Boundaries and Least Privilege](https://asibackbone.github.io/Learning/security/trust-boundaries-and-least-privilege.html) and [Secret Handling Across Trust Boundaries](https://asibackbone.github.io/Learning/security/secret-handling-across-trust-boundaries.html). NCAT's [Production Authentication Hardening Checklist](authentication-hardening.md) remains authoritative for template-specific production review.
