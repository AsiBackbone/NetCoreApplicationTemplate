using ProjectTemplate.Web.Authentication.Extensions;
using ProjectTemplate.Web.ErrorHandling;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods to configure the application's middleware pipeline
/// with a predefined, order-sensitive sequence suitable for this template.
/// </summary>
public static class PipelineExtensions
{
    /// <summary>
    /// Configures the middleware pipeline for the specified <see cref="WebApplication"/>.
    /// The ordering includes forwarded headers, request logging, exception handling,
    /// security headers, HTTPS redirection, static files, routing, rate limiting, and
    /// (optionally) authentication/authorization and endpoint mapping.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to configure.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for chaining.</returns>
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        // Keep this sequence aligned with ADR-0002 and the middleware documentation.
        // Move order-sensitive middleware only after reviewing the documented invariants.

        // 1. Proxy/load balancer correction must happen early.
        app.UseApplicationForwardedHeaders();

        // 2. Structured request logging should see corrected scheme, host, and client IP.
        app.UseApplicationRequestLogging();

        // 3. Centralized exception and status-code handling. This is the single registration: it adds the
        //    developer exception page or the production exception handler and HSTS, then branches status-code
        //    handling between Problem Details responses and the re-executed browser error page.
        app.UseProblemDetails();

        // 4. Optional security response headers.
        app.UseApplicationSecurityHeaders();

        // 5. HTTPS enforcement.
        app.UseHttpsRedirection();

        // 6. Static files before routing if using MVC/Razor UI.
        app.UseStaticFiles();

        // 7. Routing.
        app.UseRouting();

        // 8. CORS, when needed, should be after routing and before auth.
        app.UseCors();

        // 9. Rate limiting after routing when endpoint-specific policies are used.
        app.UseRateLimiter();

        // 10. Authentication and authorization.
        app.UseApplicationAuthentication();
        app.UseAuthorization();

        // 11. Endpoint mapping.
        app.MapControllers();
        app.MapRazorPages();

        return app;
    }
}
