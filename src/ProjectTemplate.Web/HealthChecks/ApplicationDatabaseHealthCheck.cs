using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProjectTemplate.Infrastructure.Data;

namespace ProjectTemplate.Web.HealthChecks;

/// <summary>
/// Reports whether the application database accepts connections.
/// </summary>
/// <remarks>
/// Registered with the <see cref="ApplicationHealthCheckTags.Ready"/> tag so an instance that cannot reach its database
/// is removed from load balancing. The result description never includes connection strings or provider exception
/// text. For SQLite, a database file that has not been created yet is reported as unavailable.
/// </remarks>
public sealed class ApplicationDatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    internal const string AvailableDescription = "The application database is reachable.";

    internal const string UnavailableDescription = "The application database is not reachable.";

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

    /// <summary>
    /// Checks whether the application database accepts connections.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">A token that cancels the check.</param>
    /// <returns>The database health result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool canConnect = await dbContext.Database
            .CanConnectAsync(cancellationToken)
            .ConfigureAwait(false);

        return canConnect
            ? HealthCheckResult.Healthy(AvailableDescription)
            : new HealthCheckResult(context.Registration.FailureStatus, UnavailableDescription);
    }
}
