# Error Handling

> **Scope:** This article is the NCAT implementation reference for generated behavior. Broader architectural rationale, alternatives, and tradeoffs live in [ASI Backbone Learning](https://asibackbone.github.io/Learning/); Learning is educational guidance, not a dependency of NCAT behavior.

The application includes centralized error handling for both unhandled exceptions and HTTP status code responses.

Error handling is configured through the application pipeline using:

```csharp
app.UseApplicationErrorHandling();
```

The error handling behavior is environment-aware:

- In Development, the application uses the developer exception page.
- In non-development environments, unhandled exceptions are routed to `/Home/Error/500`.
- HTTP status code responses are re-executed through `/Home/Error/{statusCode}` for browser-oriented requests.
- API, AJAX, and JSON-oriented requests receive Problem Details responses.
- Error responses are user-safe and do not expose exception details in production.
- Error events are logged using source-generated `LoggerMessage` methods.
- The request ID displayed on the error page matches the request ID written to the application logs.


## Implementation Locations

- Environment-aware exception/status-code middleware: [`ApplicationBuilderExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/ApplicationBuilderExtensions.cs)
- API exception conversion: [`ProblemDetailsExceptionHandler.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExceptionHandler.cs)
- Problem Details customization/classification: [`ProblemDetailsExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExtensions.cs), [`ProblemDetailsRequestClassifier.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsRequestClassifier.cs)

## Status Code Pages

Status code pages are handled centrally using:

```csharp
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
```

This allows common HTTP responses such as `404 Not Found`, `401 Unauthorized`, `403 Forbidden`, and `429 Too Many Requests` to use the shared error page strategy for browser-oriented requests.

Problem Details requests use ASP.NET Core status code pages without re-executing the browser error page.

## Unhandled Exceptions

In non-development environments, unhandled exceptions are routed through:

```csharp
app.UseExceptionHandler("/Home/Error/500");
```

This allows unhandled exceptions to be logged and displayed using the shared error page strategy without exposing sensitive exception details to end users.

## Request ID Correlation

The error page displays the same request ID that is written to the log entry.

Example browser output:

```text
Request ID: 0HNL9ADUFCPUT:00000009
```

Example log output:

```text
Status code page routed to error page. StatusCode: 404; OriginalPath: /invalid; RemoteIpAddress: ::1; RequestId: 0HNL9ADUFCPUT:00000009
```

This makes it easier to match a user-facing error page with the corresponding application log entry.

## Logging

Error handling logs include:

- Status code routed to the error page.
- Original request path.
- Remote IP address when available.
- Request ID.
- Exception details for unhandled exceptions.

Log event IDs are centralized in `ApplicationLogEventIds` to keep application logging consistent.

## Centralized Problem Details Error Handling

The application uses centralized error handling to provide consistent responses for both browser and API-style requests.

Browser requests are routed to the standard application error page, such as `/Home/Error/{statusCode}`. API, AJAX, and JSON-oriented requests receive a Problem Details response using the ASP.NET Core `IProblemDetailsService`.

Problem Details responses include safe metadata such as:

- HTTP status code.
- Error title.
- Request path.
- Trace ID.
- Request ID.

Detailed exception information is only exposed in the Development environment. Production responses avoid leaking stack traces, exception messages, connection details, secret values, provider metadata, or internal implementation information.

Unhandled exceptions are logged centrally and converted into safe Problem Details responses when the request expects JSON.

## Production Response Safety

For server-side failures in non-development environments, Problem Details responses use a generic detail message:

```text
An unexpected error occurred. Contact support with the request ID.
```

This message gives operators a correlation handle without exposing application internals to the client. Full exception details remain in server-side logs, subject to logging configuration and secret-handling policy.

See [Runtime Readiness Baseline](runtime-readiness.md) for the consolidated release-readiness view.

## Contract References

See [`ProblemDetailsExceptionHandlerTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ProblemDetailsExceptionHandlerTests.cs), [`ProblemDetailsExtensionsCustomizationTests.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/tests/ProjectTemplate.Web.Tests/ProblemDetailsExtensionsCustomizationTests.cs), and [ADR-0002](../adr/0002-use-centralized-application-middleware-pipeline.md).

## Learn the Pattern

For general centralized error-boundary design and alternatives, see [Centralized Error Handling and Problem Details](https://asibackbone.github.io/Learning/aspnetcore/centralized-error-handling-and-problem-details.html).
