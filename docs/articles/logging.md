# Logging

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

This application uses Serilog for structured application logging.

Serilog is configured as the primary logging provider so that application events, startup events, errors, and HTTP request activity are written using a consistent structured format.


## Implementation Locations

- Serilog bootstrap: [`SerilogExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/SerilogExtensions.cs)
- Request logging/correlation: [`RequestLoggingExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/RequestLoggingExtensions.cs)
- Generated sink/level/template defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)
- Architecture decision: [ADR-0001](../adr/0001-use-structured-serilog-logging.md)

## Bootstrap Logging

The application logs a bootstrap message when the web application begins provider configuration:

```csharp
Log.Information("Bootstrapping ProjectTemplate.Web application");
```

## Startup Logging

The application logs a startup message when the web application begins initialization:

```csharp
Log.Information("Starting ProjectTemplate.Web application");
```

## Pipeline Logging 

The application logs a startup message when the web application begins configuring the middleware pipeline:

```csharp
Log.Information("Configuring pipeline for ProjectTemplate.Web application");
```

## Runtime Logging

The application logs a startup message when the web application begins running:

```csharp
Log.Information("Running ProjectTemplate.Web application");
```

## Ongoing Application Logging

While the application is running, structured logs are written for normal application activity, warnings, errors, and HTTP request processing. These logs help provide visibility into the current behavior of the application without requiring a debugger to be attached.

Runtime logging may include:

- Application lifecycle events.
- Controller or endpoint activity.
- HTTP request completion details.
- Warnings from expected but noteworthy conditions.
- Exceptions and unexpected failures.
- Framework or infrastructure messages based on configured log levels.

The default logging configuration is intended to capture useful operational information while avoiding sensitive data such as passwords, authentication tokens, cookies, request bodies, and response bodies.

Additional logging can be added throughout the application by injecting `ILogger<T>` into services, controllers, middleware, or other application components.

## Production Log-Level Defaults

The default production-oriented log-level posture is configured in `appsettings.json`:

| Source | Default level |
|:---|:---|
| Application | `Information` |
| `Microsoft` | `Warning` |
| `Microsoft.AspNetCore` | `Warning` |
| `Microsoft.AspNetCore.Hosting` | `Warning` |
| `Microsoft.AspNetCore.Mvc` | `Warning` |
| `Microsoft.AspNetCore.Routing` | `Warning` |
| `System` | `Warning` |

This keeps normal application lifecycle and request activity visible while reducing framework noise. Production applications should tune these levels according to environment, hosting platform, alerting needs, and incident-response requirements.

## Structured Output Format

The console sink writes structured messages with this default template:

```text
[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] [CorrelationId: {CorrelationId}] [TraceId: {TraceId}] [SpanId: {SpanId}] [RequestId: {RequestId}] [RequestPath: {RequestPath}] {Message:lj}{NewLine}{Exception}
```

The rolling file sink writes structured messages with this default template:

```text
[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] [CorrelationId: {CorrelationId}] [TraceId: {TraceId}] [SpanId: {SpanId}] [RequestId: {RequestId}] [RequestPath: {RequestPath}] {Message:lj}{NewLine}{Exception}
```

The default file sink writes to `Logs/application-web-.log`, rolls daily, keeps 14 retained files, and rolls when the file reaches the configured size limit.

## Bootstrap Exception Logging

The application logs any exceptions that occur during the bootstrapping process or while configuring the middleware pipeline:

```csharp
Log.Fatal(ex, "ProjectTemplate.Web application terminated unexpectedly");
```

## Structured Request Logging

The application includes structured HTTP request logging through Serilog.

Request logging records a single completion event for each normal request and includes:

- HTTP method.
- Request path.
- Response status code.
- Elapsed request duration.
- Request ID.
- Correlation ID.
- Request scheme and host.
- Remote IP address, when enabled.
- Authenticated user name, when enabled.

Request logging is configured through:

```csharp
builder.Services.AddApplicationRequestLogging(builder.Configuration);
```

And applied through the standard application pipeline:

```csharp
app.UseApplicationRequestLogging();
```

Configuration is controlled through `appsettings.json`:

```json
"ProjectTemplate": {
  "RequestLogging": {
    "Enabled": true,
    "CorrelationHeaderName": "X-Correlation-ID",
    "IncludeQueryString": false,
    "IncludeUserName": true,
    "IncludeRemoteIpAddress": true,
    "ExcludedPathPrefixes": [
      "/health",
      "/metrics",
      "/favicon.ico",
      "/css",
      "/js",
      "/lib",
      "/_framework"
    ]
  }
}
```

Query string logging is disabled by default because query strings may contain sensitive values. Applications should avoid logging request bodies, response bodies, cookies, authorization headers, access tokens, refresh tokens, or authentication payloads unless a specific, reviewed diagnostic need exists.

See [Runtime Readiness Baseline](runtime-readiness.md) for the consolidated release-readiness view.

## Contract References

[`RequestLoggingExtensionsBranchGapTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/RequestLoggingExtensionsBranchGapTests.cs) exercises request-logging branches. `appsettings.json` is authoritative for generated sink, level, retention, and output-template defaults.

## Learn the Pattern

See [Structured Logging Without Sensitive-Data Sprawl](https://asibackbone.github.io/Learning/aspnetcore/structured-logging-without-sensitive-data-sprawl.html) and [Secure Logging Across Trust Boundaries](https://asibackbone.github.io/Learning/security/secure-logging-across-trust-boundaries.html).
