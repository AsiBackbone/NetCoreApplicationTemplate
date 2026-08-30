# Telemetry

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application includes baseline [OpenTelemetry](https://opentelemetry.io/) support for tracing and metrics.

OpenTelemetry is registered through:

```csharp
builder.Services.AddApplicationOpenTelemetry(builder.Configuration, builder.Environment);
```

Configuration is controlled through `appsettings.json`:

```json
"ProjectTemplate": {
  "OpenTelemetry": {
    "Enabled": true,
    "ServiceName": "ProjectTemplate.Web",
    "EnableTracing": true,
    "EnableMetrics": true,
    "EnableAspNetCoreInstrumentation": true,
    "EnableHttpClientInstrumentation": true,
    "Otlp": {
      "Enabled": false,
      "Endpoint": "",
      "Protocol": "Grpc"
    }
  }
}
```

By default, the application collects local tracing and metrics instrumentation but does not export telemetry to an external collector. To enable OTLP export, configure an OTLP endpoint and set `ProjectTemplate:OpenTelemetry:Otlp:Enabled` to `true`.

Common local OTLP collector endpoints:

```text
http://localhost:4317
http://localhost:4318
```

The OTLP exporter can also be configured through standard OpenTelemetry environment variables such as `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL`.


## Implementation Locations

- OpenTelemetry registration/instrumentation/OTLP export: [`OpenTelemetryServiceExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/OpenTelemetryServiceExtensions.cs)
- Option models: [`ApplicationOpenTelemetryOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Options/ApplicationOpenTelemetryOptions.cs), [`ApplicationOtlpExporterOptions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Options/ApplicationOtlpExporterOptions.cs)
- Generated defaults: [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)

## Runtime Baseline

The telemetry baseline is:

- OpenTelemetry service/resource registration.
- ASP.NET Core request tracing instrumentation.
- HTTP client tracing instrumentation.
- ASP.NET Core metrics instrumentation.
- HTTP client metrics instrumentation.
- Optional OTLP trace and metric export.
- Environment tagging through `deployment.environment.name`.

The template validates the configured service name and validates the OTLP endpoint when OTLP export is enabled.

## W3C Trace Context

The template relies on ASP.NET Core and OpenTelemetry defaults for request activity creation and trace propagation. It does not override the default propagator or force a custom trace identifier format.

When an incoming request includes a valid trace context, the ASP.NET Core and OpenTelemetry pipeline can attach server-side activity to that context. Error responses that use Problem Details include a `traceId` value from `Activity.Current?.Id` when available, falling back to the ASP.NET Core request trace identifier.

This gives operators a consistent path from an API error response to application logs and telemetry traces.

## Log and Problem Details Correlation

The template keeps request correlation, logs, Problem Details, and OpenTelemetry traces connected through a small shared identifier set:

- `correlationId` comes from the configured request correlation header, defaulting to `X-Correlation-ID`.
- `requestId` comes from ASP.NET Core `HttpContext.TraceIdentifier`.
- `traceId` and `spanId` come from the current W3C `Activity` when available.
- Serilog request logs include the same identifiers so operators can move from an error response to logs and then to an OpenTelemetry trace.

OTLP export remains disabled by default. Applications can enable trace and metric export by configuring `ProjectTemplate:OpenTelemetry:Otlp`.

## Metrics Endpoint Behavior

The template does not expose a direct Prometheus `/metrics` scraping endpoint by default.

Metrics are collected through OpenTelemetry instrumentation and can be exported through OTLP when an OTLP collector endpoint is configured. Prometheus support should be added through one of these deployment-specific approaches:

- Configure an OpenTelemetry Collector to receive OTLP and expose Prometheus-compatible scraping.
- Add a Prometheus-compatible OpenTelemetry exporter in the application through a dedicated implementation change.
- Add a deliberate application-specific metrics endpoint with security, routing, and deployment review.

A direct scraping endpoint should not be added casually because it may expose operational metadata and may need network restrictions, authentication, request logging exclusions, and security-header review.

See [Runtime Readiness Baseline](runtime-readiness.md) for the consolidated release-readiness view.

## Contract References

[`OpenTelemetryTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/OpenTelemetryTests.cs) and [`OpenTelemetryServiceExtensionsCoverageTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/OpenTelemetryServiceExtensionsCoverageTests.cs) establish the generated registration/configuration contract. Problem Details tests cover the error-response side of trace/request correlation.

## Learn the Pattern

For broader observability event design and trust-boundary handling, see [Structured Logging Without Sensitive-Data Sprawl](https://asibackbone.github.io/Learning/aspnetcore/structured-logging-without-sensitive-data-sprawl.html) and [Secure Logging Across Trust Boundaries](https://asibackbone.github.io/Learning/security/secure-logging-across-trust-boundaries.html).
