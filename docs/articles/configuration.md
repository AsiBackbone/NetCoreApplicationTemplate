# Configuration

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

This section documents the application configuration strategy used by the .NET Core Application Template.

The template uses ASP.NET Core configuration conventions and groups application-owned options under the `ProjectTemplate` configuration section. Shared defaults live in `appsettings.json`, while environment-specific files, environment variables, user secrets, or deployment secret stores can override values for a specific environment.


## Implementation Locations

- Shared generated defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)
- Development overrides: [`appsettings.Development.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.Development.json)
- Web option models: [`src/ProjectTemplate.Web/Options`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/src/ProjectTemplate.Web/Options)
- Authentication option models: [`Authentication/Options`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/src/ProjectTemplate.Web/Authentication/Options)
- Data-access options: [`DataAccessOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Infrastructure/Data/Options/DataAccessOptions.cs)

## Configuration Sources

Common configuration sources include:

- `appsettings.json` for shared defaults.
- `appsettings.Development.json` for local development overrides.
- `appsettings.Production.json` or platform-provided configuration for production overrides.
- Environment variables for deployment-specific values.
- User secrets for local-only sensitive development values.
- External secret stores or hosting platform secret managers for production secrets.

The repository includes safe example files under `docs/examples`:

- [`appsettings.Development.example.json`](../examples/appsettings.Development.example.json)
- [`appsettings.Production.example.json`](../examples/appsettings.Production.example.json)

These files are examples only. They should be copied and adapted by consuming applications, not treated as production-ready configuration.

## Configuration Precedence

ASP.NET Core configuration is layered. Later configuration providers can override earlier providers.

A typical application order is:

```text
1. appsettings.json
2. appsettings.{Environment}.json
3. User secrets, usually Development only
4. Environment variables
5. Command-line arguments
```

This means a value from an environment variable can override the same value from `appsettings.json` or `appsettings.Production.json`.

For example, this JSON value:

```json
{
  "ProjectTemplate": {
    "RateLimiting": {
      "GlobalFixedWindow": {
        "PermitLimit": 60
      }
    }
  }
}
```

Can be overridden by this environment variable:

```powershell
$env:ProjectTemplate__RateLimiting__GlobalFixedWindow__PermitLimit = "120"
```

Double underscores map to nested configuration keys. This is useful for containers, cloud hosting platforms, CI/CD systems, and deployment pipelines.

## Development Configuration Example

Development configuration should favor local productivity while avoiding real production secrets.

Common development overrides include:

- Local SQLite database connection strings.
- LocalDB SQL Server examples.
- Debug-level logging.
- Shorter log retention.
- Disabled or relaxed CSP while debugging local assets.
- Disabled external authentication providers unless actively testing them.
- Local-only provider client IDs and secrets stored with user secrets.

See [`appsettings.Development.example.json`](../examples/appsettings.Development.example.json) for a safe development-oriented example.

## Production Configuration Example

Production configuration should be explicit, conservative, and environment-specific.

Common production overrides include:

- Real allowed host names.
- Trusted forwarded header proxy addresses or networks.
- Strong Content Security Policy values.
- Production logging retention expectations.
- Production rate limit values.
- SQL Server or other production data provider settings.
- External authentication provider settings.
- Secret values supplied from the environment or secret store instead of committed JSON.

See [`appsettings.Production.example.json`](../examples/appsettings.Production.example.json) for a safe production-oriented example. The example intentionally leaves secrets and connection strings blank.

## Environment Variable Overrides

Environment variables are useful when configuration must vary by deployment slot, container, host, or pipeline.

Examples:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AllowedHosts = "example.com"
$env:ConnectionStrings__ApplicationSqlServer = "Server=tcp:sql.example.com,1433;Database=Application;Encrypt=True;TrustServerCertificate=False;"
$env:ProjectTemplate__DataAccess__Provider = "SqlServer"
$env:ProjectTemplate__DataAccess__ConnectionStringName = "ApplicationSqlServer"
$env:ProjectTemplate__DataAccess__Auditing__StorageMode = "Local"
$env:ProjectTemplate__SecurityHeaders__EnableContentSecurityPolicy = "true"
$env:ProjectTemplate__RateLimiting__GlobalFixedWindow__PermitLimit = "120"
```

For Linux shells or container environments, the same keys are usually set without `$env:`:

```bash
export ASPNETCORE_ENVIRONMENT="Production"
export ProjectTemplate__DataAccess__Provider="SqlServer"
export ProjectTemplate__DataAccess__Auditing__StorageMode="Local"
export ProjectTemplate__RateLimiting__GlobalFixedWindow__PermitLimit="120"
```

## Secret Management Guidance

Do not commit production secrets to the repository.

Sensitive values include:

- Production database connection strings.
- External authentication client secrets.
- API keys.
- Signing certificates or certificate passwords.
- Access tokens.
- SMTP credentials.
- Any organization-specific private endpoint values.

For local development, prefer user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:ApplicationSqlServer" "Server=(localdb)\MSSQLLocalDB;Database=Application;Trusted_Connection=True;TrustServerCertificate=True" --project src/ProjectTemplate.Web

dotnet user-secrets set "ProjectTemplate:Authentication:Providers:GitHub:ClientSecret" "local-development-secret" --project src/ProjectTemplate.Web
```

The generated web project includes a `UserSecretsId` so local development secrets can be managed through Visual Studio's **Manage User Secrets** command or the `dotnet user-secrets` CLI. User secrets are for local development only and should not be treated as encrypted production storage.

Production deployments should supply sensitive values through protected environment variables, hosting-platform secret managers, or vault-backed configuration providers. Do not place production connection strings, authentication provider secrets, API keys, signing material, or organization-specific private endpoints in committed `appsettings*.json` files.

## Provider-Specific Configuration

The template supports configurable data access provider selection through `ProjectTemplate:DataAccess`.

SQLite development example:

```json
{
  "ConnectionStrings": {
    "ApplicationDatabase": "Data Source=application-dev.db"
  },
  "ProjectTemplate": {
    "DataAccess": {
      "Provider": "Sqlite",
      "ConnectionStringName": "ApplicationDatabase",
      "Auditing": {
        "Enabled": true,
        "StorageMode": "Local"
      }
    }
  }
}
```

SQL Server production-style example:

```json
{
  "ConnectionStrings": {
    "ApplicationSqlServer": ""
  },
  "ProjectTemplate": {
    "DataAccess": {
      "Provider": "SqlServer",
      "ConnectionStringName": "ApplicationSqlServer",
      "Auditing": {
        "Enabled": true,
        "StorageMode": "Local"
      }
    }
  }
}
```

In production, the actual `ApplicationSqlServer` connection string should come from the environment or secret store.

`ProjectTemplate:DataAccess:Auditing:StorageMode` defaults to `Local`. `Outbox` and `ExternalSink` are supported extension-mode names, but they require a custom `IApplicationAuditStore` registration supplied by the consuming application.

## Configuration Validation

The application uses strongly typed options classes for application-owned configuration under the `ProjectTemplate` section.

Validated configuration areas include:

- `ProjectTemplate:ApiVersioning`
- `ProjectTemplate:Authentication`
- `ProjectTemplate:Authorization`
- `ProjectTemplate:DataAccess`
- `ProjectTemplate:ForwardedHeaders`
- `ProjectTemplate:OpenTelemetry`
- `ProjectTemplate:RateLimiting`
- `ProjectTemplate:RequestLogging`
- `ProjectTemplate:SecurityHeaders`

Invalid startup-sensitive values fail application startup rather than being silently corrected. This helps catch unsafe or malformed production configuration before the application begins serving requests.

## Production Review Checklist

Before production release, review:

```text
[ ] ASPNETCORE_ENVIRONMENT is set correctly.
[ ] AllowedHosts matches the public host names.
[ ] Forwarded header settings match the proxy or load balancer topology.
[ ] Production connection strings come from environment or secret storage.
[ ] Data access provider, audit storage mode, and audit retention/export expectations are documented.
[ ] External authentication provider secrets are not committed.
[ ] Content Security Policy values are tested against deployed assets and auth flows.
[ ] Rate limit values are tuned for expected traffic.
[ ] Logging destinations, retention, and sensitive-data exposure are reviewed.
[ ] Database provider and migration strategy are documented for the environment.
```

## Contract References

Feature-specific startup-validation tests establish the generated configuration contract, including authentication, forwarded headers, rate limiting, OpenTelemetry, and data-access tests.

## Learn the Pattern

For broader configuration and secret-management reasoning, see [Secure-by-Default ASP.NET Core Configuration](https://asibackbone.github.io/Learning/aspnetcore/secure-by-default-configuration.html) and [Secret Handling Across Trust Boundaries](https://asibackbone.github.io/Learning/security/secret-handling-across-trust-boundaries.html).
