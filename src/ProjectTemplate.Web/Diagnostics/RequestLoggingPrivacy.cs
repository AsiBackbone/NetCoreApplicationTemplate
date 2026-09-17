using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Diagnostics;

/// <summary>
/// Applies the request-logging privacy options to diagnostic log entries written outside the request-logging
/// middleware, such as rate-limit rejection and error-page log entries.
/// </summary>
/// <remarks>
/// <see cref="ApplicationRequestLoggingOptions.IncludeRemoteIpAddress"/> is the single application-wide decision about
/// whether client IP addresses are written to logs. Every log entry that would otherwise record the remote IP address
/// should obtain it through this class so the decision is honored consistently.
/// </remarks>
internal static class RequestLoggingPrivacy
{
    /// <summary>
    /// Gets the remote IP address to write to a log entry, or <see langword="null"/> when remote IP address logging is
    /// not enabled.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns>
    /// The remote IP address when <see cref="ApplicationRequestLoggingOptions.IncludeRemoteIpAddress"/> is
    /// <see langword="true"/> and an address is available; otherwise <see langword="null"/>. When the options are not
    /// registered, the privacy-preserving default applies and <see langword="null"/> is returned.
    /// </returns>
    internal static string? GetLoggableRemoteIpAddress(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ApplicationRequestLoggingOptions? options = httpContext.RequestServices?
            .GetService<IOptions<ApplicationRequestLoggingOptions>>()?
            .Value;

        return options?.IncludeRemoteIpAddress == true
            ? httpContext.Connection.RemoteIpAddress?.ToString()
            : null;
    }
}
