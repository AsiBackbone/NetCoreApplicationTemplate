using ProjectTemplate.Web.Authentication.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides startup-only diagnostics for security-relevant supported deployment postures.
/// </summary>
public static class StartupSecurityPostureExtensions
{
    private const string _authenticationEnabledConfigurationKey =
        ApplicationAuthenticationOptions.SectionName + ":Enabled";

    private const string _anonymousHealthEndpoints =
        "/health, /health/ready, /health/live";

    private static readonly Action<ILogger, string, Exception?> _logAuthenticationDisabled =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1001, "AuthenticationDisabled"),
            "Security posture: authentication is intentionally disabled by {ConfigurationKey}. " +
            "This is a supported configuration, not an authentication framework failure. " +
            "Review deployment exposure and authorization expectations before production use.");

    private static readonly Action<ILogger, string, Exception?> _logAnonymousProductionHealthEndpoints =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, "AnonymousProductionHealthEndpoints"),
            "Security posture: health endpoints {HealthEndpoints} are intentionally mapped with anonymous access in Production. " +
            "Anonymous probes are supported for infrastructure health checks; confirm reverse-proxy, ingress, firewall, " +
            "or service-mesh routing limits external reachability as intended.");

    /// <summary>
    /// Emits structured startup diagnostics for supported security postures that require
    /// operator awareness.
    /// </summary>
    /// <param name="app">The application whose startup posture is being reported.</param>
    /// <returns>The original <see cref="WebApplication"/> for chaining.</returns>
    public static WebApplication LogApplicationSecurityPosture(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        LogApplicationSecurityPosture(app.Logger, app.Configuration, app.Environment);

        return app;
    }

    /// <summary>
    /// Emits structured startup diagnostics using the supplied application services.
    /// This overload keeps the posture rules independently testable.
    /// </summary>
    /// <param name="logger">The startup logger.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    public static void LogApplicationSecurityPosture(
        ILogger logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        bool authenticationEnabled = configuration.GetValue<bool>(
            _authenticationEnabledConfigurationKey);

        if (!authenticationEnabled)
        {
            _logAuthenticationDisabled(
                logger,
                _authenticationEnabledConfigurationKey,
                null);
        }

        if (environment.IsProduction())
        {
            _logAnonymousProductionHealthEndpoints(
                logger,
                _anonymousHealthEndpoints,
                null);
        }
    }
}
