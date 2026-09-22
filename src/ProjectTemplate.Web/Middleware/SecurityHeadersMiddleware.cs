using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Middleware;

/// <summary>
/// Middleware that applies a set of security-related HTTP headers to responses.
/// </summary>
/// <remarks>
/// The middleware can be configured via <see cref="ApplicationSecurityHeadersOptions"/> to enable/disable
/// individual headers and to exclude certain request path prefixes.
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<ApplicationSecurityHeadersOptions> options)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ApplicationSecurityHeadersOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Invokes the middleware for the given <paramref name="context"/>, applying configured
    /// security headers to the response when appropriate.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A <see cref="Task"/> that completes when the middleware and the next delegate finish processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        if (IsExcludedPath(context.Request.Path))
        {
            // Excluded endpoints (health, metrics) skip the document-oriented headers, but MIME sniffing
            // protection costs nothing and applies to every response.
            context.Response.OnStarting(() =>
            {
                AddHeaderIfMissing(context.Response.Headers, "X-Content-Type-Options", "nosniff");
                return Task.CompletedTask;
            });

            await _next(context);
            return;
        }

        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            AddHeaderIfMissing(headers, "X-Content-Type-Options", "nosniff");
            AddHeaderIfMissing(headers, "X-Frame-Options", "DENY");
            AddHeaderIfMissing(headers, "Referrer-Policy", "strict-origin-when-cross-origin");
            AddHeaderIfMissing(headers, "X-Permitted-Cross-Domain-Policies", "none");

            // Modern browser isolation / cross-origin protections.
            if (_options.EnableCrossOriginHeaders)
            {
                AddHeaderIfMissing(headers, "Cross-Origin-Opener-Policy", "same-origin");
                AddHeaderIfMissing(headers, "Cross-Origin-Resource-Policy", "same-origin");
            }

            if (_options.EnablePermissionsPolicy &&
                !string.IsNullOrWhiteSpace(_options.PermissionsPolicy))
            {
                AddHeaderIfMissing(headers, "Permissions-Policy", _options.PermissionsPolicy);
            }

            if (_options.EnableContentSecurityPolicy &&
                !string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy))
            {
                AddHeaderIfMissing(headers, "Content-Security-Policy", _options.ContentSecurityPolicy);
            }

            // Do not add X-XSS-Protection. It is obsolete and can cause problems in some browsers.

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private bool IsExcludedPath(PathString path)
    {
        return _options.ExcludedPathPrefixes.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddHeaderIfMissing(
        IHeaderDictionary headers,
        string name,
        string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}

