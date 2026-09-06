# API Versioning

The application includes a foundation for controller-based API versioning.

API versioning helps keep public API routes stable while allowing future versions to evolve without breaking existing clients.

## Default Version

The default API version is configured under `ProjectTemplate:ApiVersioning`.

```json
{
  "ProjectTemplate": {
    "ApiVersioning": {
      "DefaultMajorVersion": 1,
      "DefaultMinorVersion": 0,
      "AssumeDefaultVersionWhenUnspecified": true,
      "ReportApiVersions": true,
      "EnableUrlSegmentVersioning": true,
      "EnableHeaderVersioning": true,
      "HeaderName": "X-API-Version",
      "DeprecatedVersionSunset": null
    }
  }
}
```
The application starts with API version `1.0`.

## Supported Versioning Strategies

The application supports both URL segment and request header versioning.

URL segment example:
```http
GET /api/v1/application-information
```
Header versioning example:
```http
GET /api/application-information
X-API-Version: 1.0
```
URL segment versioning is usually easier to discover and debug. Header versioning can be useful for clients that prefer stable resource URLs.

## Controller Convention

Versioned API controllers should declare the supported API version and include a versioned route.
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/example")]
public sealed class ExampleController : ControllerBase
{
}
```
If an endpoint should also support header-based versioning without the version in the URL, add an unversioned route as well.
```csharp
[Route("api/example")]
```

## Response Headers

When `ReportApiVersions` is enabled, responses include API version headers that help clients discover supported and deprecated versions.

A request for a deprecated version also receives `Deprecation: true` and a `Link` header naming the successor version.

The `Sunset` header is advertised only when `DeprecatedVersionSunset` is configured, and is omitted otherwise. A sunset date commits to callers about when a version stops being served, so the template ships none: a date hard-coded here is arbitrary while it is in the future, and misleading once it passes, because every generated application would advertise a removal that already happened. Set it when the application actually retires a version:

```json
"DeprecatedVersionSunset": "2027-06-30T23:59:59+00:00"
```

The value is emitted in RFC 1123 form, as the `Sunset` header requires.

## Release Compatibility

URL segment versioning is the canonical API versioning strategy for the template. The default route convention is:

```text
/api/v{version}/<resource>
```

Header-based versioning with `X-API-Version` remains available as a secondary compatibility option for applications that need stable resource URLs.

After the `v1.0.0` release, changes to documented API route conventions, default API version behavior, or the canonical versioning strategy should be reviewed as release-surface changes. Removing or changing documented route conventions may require a major version. Adding compatible route forms or additional supported API versions may be treated as minor version work.

See [ADR-0003: Record Release Surface and Distribution Strategy](../adr/0003-record-release-surface-and-distribution-strategy.md) for the release-surface decision.

## Production Guidance

For production APIs:
- Prefer a consistent versioning strategy across the application.
- Avoid mixing URL and header versioning on the same endpoint unless clients need both.
- Keep old API versions available long enough for client migration.
- Document deprecated versions before removing them.
- Add tests for each supported API version.

