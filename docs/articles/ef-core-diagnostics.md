# EF Core Diagnostics

NetCoreApplicationTemplate keeps optional EF Core diagnostics disabled by default so normal application logging remains efficient and production-oriented.

EF Core already integrates with `Microsoft.Extensions.Logging`. Normal EF Core categories such as `Microsoft.EntityFrameworkCore.Database.Command` continue to follow the application's configured logging levels without enabling any additional diagnostics in the data-access layer.

## Configuration

Optional diagnostics are configured beneath `ProjectTemplate:DataAccess:Diagnostics`:

```json
{
  "ProjectTemplate": {
    "DataAccess": {
      "Diagnostics": {
        "EnableDetailedErrors": false,
        "EnableEfCoreTraceBridge": false
      }
    }
  }
}
```

Both settings default to `false` when omitted.

| Setting | Default | Purpose |
|:--|:--:|:--|
| `EnableDetailedErrors` | `false` | Calls EF Core `EnableDetailedErrors()` for the configured `ApplicationDbContext`. This can improve property-level exception diagnostics but adds diagnostic work during query materialization. |
| `EnableEfCoreTraceBridge` | `false` | Enables EF Core simple logging through `LogTo(...)` and forwards those messages through the application `ILogger<ApplicationDbContext>` at `Trace` level using event ID `19000`. |

The settings apply consistently to both scoped `ApplicationDbContext` instances and contexts created through `IDbContextFactory<ApplicationDbContext>`.

## Recommended Operational Use

Leave both settings disabled during normal operation and use standard logging configuration first. For example, EF Core category verbosity can be changed with the application's normal logging provider configuration without enabling the custom trace bridge.

Enable one of the diagnostic settings temporarily when investigating a specific data-access problem and disable it after the investigation. Configuration can be supplied through the normal ASP.NET Core configuration providers, including environment-specific JSON, environment variables, user secrets, or deployment configuration.

Environment variables use the normal double-underscore mapping. For example:

```text
ProjectTemplate__DataAccess__Diagnostics__EnableDetailedErrors=true
ProjectTemplate__DataAccess__Diagnostics__EnableEfCoreTraceBridge=true
```

`EnableEfCoreTraceBridge` is intentionally independent from the hosting environment. This allows an operator to turn on temporary diagnostics in a controlled production deployment without rebuilding the application or pretending that the deployment is a Development environment.

## Detailed Errors vs. Sensitive-Data Logging

`EnableDetailedErrors()` and `EnableSensitiveDataLogging()` are separate EF Core features.

Enabling `ProjectTemplate:DataAccess:Diagnostics:EnableDetailedErrors` does **not** enable sensitive-data logging. The template does not enable `EnableSensitiveDataLogging()` through these settings, and sensitive-data logging remains disabled unless a consuming application explicitly introduces and configures that behavior itself.

This distinction matters in production because sensitive-data logging can expose application values in diagnostic output. Do not treat the detailed-errors option as permission to log confidential, regulated, credential, token, or personally identifiable values.

## Trace Bridge vs. Standard EF Core Logging

The optional trace bridge exists for cases where an operator specifically wants EF Core's simple `LogTo(...)` stream routed through the application's `ILogger<ApplicationDbContext>` category.

It is disabled by default because standard EF Core logging already flows through `Microsoft.Extensions.Logging`, while a second `LogTo(...)` pipeline can cause EF Core to format additional diagnostic messages and can duplicate information already available through normal EF Core categories.

Prefer normal logging-category configuration for ongoing observability. Treat the trace bridge as a temporary troubleshooting tool.

## Save-Changes Interceptor

The diagnostics settings do not control the template's `ApplicationSaveChangesInterceptor`.

When EF Core data access is enabled, the save-changes interceptor remains registered and attached regardless of whether detailed errors or the trace bridge are enabled. Disabling optional diagnostics therefore does not remove auditing, canonicalization, mutation-manifest, or other save-pipeline behavior owned by the interceptor and save-changes pipeline.
