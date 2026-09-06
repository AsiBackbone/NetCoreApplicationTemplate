using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProjectTemplate.Web.Constants;
using ProjectTemplate.Web.Models;

namespace ProjectTemplate.Web.Controllers;

/// <summary>
/// Controller for handling the home page and error routes.
/// </summary>
/// <param name="logger">The logger instance for the controller.</param>
public partial class HomeController(ILogger<HomeController> logger) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;

    /// <summary>
    /// Displays an error page for a request routed here by the error-handling pipeline. The response status
    /// code comes from the pipeline rather than the route value, and a request that did not arrive through
    /// the pipeline is answered with 404 instead of the error page.
    /// </summary>
    /// <remarks>
    /// This route is anonymous by necessity, since it serves errors for unauthenticated requests. Trusting its
    /// route value would let any caller choose the response status code — including values outside the valid
    /// range, such as <c>/Home/Error/0</c> or <c>/Home/Error/999</c> — and emit a warning-level log entry per
    /// request. The presence of <see cref="IStatusCodeReExecuteFeature" /> or
    /// <see cref="IExceptionHandlerPathFeature" /> is what distinguishes a re-executed request from a direct one.
    /// </remarks>
    /// <param name="statusCode">Status code supplied by the status-code re-execute route. Ignored for direct requests.</param>
    /// <returns>The error view for a pipeline-routed request; otherwise a 404 result.</returns>
    [AllowAnonymous]
    [Route("Home/Error/{statusCode:int?}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        IExceptionHandlerPathFeature? exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        IStatusCodeReExecuteFeature? statusCodeFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

        // A request that reaches this route directly carries neither feature. It is answered as not found, so
        // the route value cannot select the response status and cannot generate error-page log entries.
        if (exceptionFeature is null && statusCodeFeature is null)
        {
            return NotFound();
        }

        int resolvedStatusCode = ResolveStatusCode(statusCodeFeature, statusCode);

        Response.StatusCode = resolvedStatusCode;

        string originalPath =
            exceptionFeature?.Path ??
            statusCodeFeature?.OriginalPath ??
            HttpContext.Request.Path.ToString();

        string? remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        string requestId = HttpContext.TraceIdentifier;

        if (exceptionFeature?.Error is not null)
        {
            LogUnhandledExceptionRoutedToErrorPage(
                _logger,
                exceptionFeature.Error,
                resolvedStatusCode,
                originalPath,
                remoteIpAddress,
                requestId);
        }
        else
        {
            LogStatusCodePageRoutedToErrorPage(
                _logger,
                resolvedStatusCode,
                originalPath,
                remoteIpAddress,
                requestId);
        }

        return View(new ErrorViewModel
        {
            StatusCode = resolvedStatusCode,
            RequestId = requestId
        });
    }

    /// <summary>
    /// Resolves the status code to return, preferring the value the pipeline recorded over the route value.
    /// </summary>
    /// <param name="statusCodeFeature">The status-code re-execute feature, when the request was re-executed.</param>
    /// <param name="routeStatusCode">The status code bound from the route.</param>
    /// <returns>A status code inside the range the error page serves.</returns>
    private static int ResolveStatusCode(IStatusCodeReExecuteFeature? statusCodeFeature, int? routeStatusCode)
    {
        int resolvedStatusCode = statusCodeFeature?.OriginalStatusCode
            ?? routeStatusCode
            ?? StatusCodes.Status500InternalServerError;

        // Status-code pages re-execute for client and server errors only. Anything outside that range means the
        // value did not come from the pipeline, so it is not written to the response.
        return resolvedStatusCode is < StatusCodes.Status400BadRequest or > 599
            ? StatusCodes.Status500InternalServerError
            : resolvedStatusCode;
    }

    [LoggerMessage(
        EventId = ApplicationLogEventIds.UnhandledExceptionRoutedToErrorPage,
        Level = LogLevel.Error,
        Message = "Unhandled exception routed to error page. StatusCode: {StatusCode}; OriginalPath: {OriginalPath}; RemoteIpAddress: {RemoteIpAddress}; TraceIdentifier: {TraceIdentifier}")]
    private static partial void LogUnhandledExceptionRoutedToErrorPage(
        ILogger logger,
        Exception exception,
        int statusCode,
        string originalPath,
        string? remoteIpAddress,
        string traceIdentifier);

    [LoggerMessage(
        EventId = ApplicationLogEventIds.StatusCodePageRoutedToErrorPage,
        Level = LogLevel.Warning,
        Message = "Status code page routed to error page. StatusCode: {StatusCode}; OriginalPath: {OriginalPath}; RemoteIpAddress: {RemoteIpAddress}; TraceIdentifier: {TraceIdentifier}")]
    private static partial void LogStatusCodePageRoutedToErrorPage(
        ILogger logger,
        int statusCode,
        string originalPath,
        string? remoteIpAddress,
        string traceIdentifier);
}
