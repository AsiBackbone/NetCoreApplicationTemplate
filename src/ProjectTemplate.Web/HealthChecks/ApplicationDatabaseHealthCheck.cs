using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.HealthChecks;

/// <summary>
/// Reports whether the application database accepts connections.
/// </summary>
/// <remarks>
/// Registered with the <see cref="ApplicationHealthCheckTags.Ready"/> tag so an instance that cannot reach its database
/// is removed from load balancing. The result description never includes connection strings or provider exception
/// text. For SQLite, a database file that has not been created yet is reported as unavailable. When
/// <see cref="ApplicationHealthCheckOptions.DatabaseReadinessCheckEnabled"/> is <see langword="false"/>, the check
/// reports healthy without contacting the database.
/// </remarks>
public sealed class ApplicationDatabaseHealthCheck(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ApplicationHealthCheckOptions> options)
    : IHealthCheck
{
    internal const string AvailableDescription = "The application database is reachable.";

    internal const string UnavailableDescription = "The application database is not reachable.";

    internal const string DisabledDescription = "The application database readiness check is disabled.";

    private readonly IServiceScopeFactory _scopeFactory =
        scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly IOptionsMonitor<ApplicationHealthCheckOptions> _options =
        options ?? throw new ArgumentNullException(nameof(options));

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

        if (!_options.CurrentValue.DatabaseReadinessCheckEnabled)
        {
            return HealthCheckResult.Healthy(DisabledDescription);
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (IsMissingSqliteDatabase(dbContext))
        {
            return new HealthCheckResult(context.Registration.FailureStatus, UnavailableDescription);
        }

        bool canConnect = await dbContext.Database
            .CanConnectAsync(cancellationToken)
            .ConfigureAwait(false);

        return canConnect
            ? HealthCheckResult.Healthy(AvailableDescription)
            : new HealthCheckResult(context.Registration.FailureStatus, UnavailableDescription);
    }

    private static bool IsMissingSqliteDatabase(ApplicationDbContext dbContext)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            return false;
        }

        string dataSource = dbContext.Database.GetDbConnection().DataSource;

        return !string.IsNullOrWhiteSpace(dataSource) &&
            !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) && !File.Exists(dataSource);
    }
}
