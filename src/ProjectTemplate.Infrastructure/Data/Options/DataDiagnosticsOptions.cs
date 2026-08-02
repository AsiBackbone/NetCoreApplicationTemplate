namespace ProjectTemplate.Infrastructure.Data.Options;

/// <summary>
/// Controls optional EF Core diagnostics for application data access.
/// </summary>
public sealed class DataDiagnosticsOptions
{
    /// <summary>
    /// Gets a value indicating whether EF Core detailed errors are enabled.
    /// </summary>
    public bool EnableDetailedErrors { get; init; }

    /// <summary>
    /// Gets a value indicating whether EF Core simple logging is bridged into the application logger at Trace level.
    /// </summary>
    public bool EnableEfCoreTraceBridge { get; init; }
}
