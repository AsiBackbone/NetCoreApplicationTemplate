using ProjectTemplate.Web.Middleware;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods to register and enable security headers functionality.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Registers the <see cref="ApplicationSecurityHeadersOptions"/> configuration section with the DI container.
    /// </summary>
    /// <param name="services">The service collection to add the configuration to.</param>
    /// <param name="configuration">The application configuration containing the "SecurityHeaders" section.</param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddApplicationSecurityHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ApplicationSecurityHeadersOptions>()
            .Bind(configuration.GetSection(ApplicationSecurityHeadersOptions.SectionName))
            .Validate(
                options =>
                    !options.EnableContentSecurityPolicy ||
                    !string.IsNullOrWhiteSpace(options.ContentSecurityPolicy),
                "ProjectTemplate:SecurityHeaders:ContentSecurityPolicy is required when CSP is enabled.")
            .Validate(
                options =>
                    !options.EnablePermissionsPolicy ||
                    !string.IsNullOrWhiteSpace(options.PermissionsPolicy),
                "ProjectTemplate:SecurityHeaders:PermissionsPolicy is required when Permissions-Policy is enabled.")
            .Validate(
                options =>
                    options.ExcludedPathPrefixes.All(path =>
                        !string.IsNullOrWhiteSpace(path) &&
                        path.StartsWith('/')),
                "ProjectTemplate:SecurityHeaders:ExcludedPathPrefixes values must start with '/'.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds the security headers middleware to the application's request pipeline.
    /// </summary>
    /// <param name="app">The application builder used to configure the request pipeline.</param>
    /// <returns>The original <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseApplicationSecurityHeaders(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>
    /// Adds HTTP Strict Transport Security (HSTS) outside the development environment.
    /// </summary>
    /// <remarks>
    /// HSTS is skipped in development so browsers do not pin localhost to HTTPS. <c>UseProblemDetails</c> registers
    /// it between the exception handler and the status-code page branches, ahead of HTTPS redirection.
    /// </remarks>
    /// <param name="app">The <see cref="WebApplication"/> used to configure the request pipeline.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for chaining.</returns>
    public static WebApplication UseApplicationHsts(
        this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        return app;
    }
}

