using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Infrastructure.Data.Services;

internal sealed partial class DataAccessStartupLogger(
    ILogger<DataAccessStartupLogger> logger,
    IOptions<DataAccessOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (DataAccessOptions.IsDisabledProvider(options.Value.Provider))
        {
            LogDataAccessDisabled(
                logger,
                options.Value.Provider);

            return Task.CompletedTask;
        }

        LogDataAccessConfiguration(
            logger,
            options.Value.Provider,
            options.Value.ConnectionStringName,
            options.Value.Auditing.Enabled ? "enabled" : "disabled",
            options.Value.Auditing.StorageMode);

        bool hasMigrationHistory = typeof(ApplicationDbContext).Assembly
            .GetTypes()
            .Any(static type =>
                !type.IsAbstract
                && typeof(Migration).IsAssignableFrom(type));

        LogSqlServerMigrationPosture(
            logger,
            options.Value.Provider,
            hasMigrationHistory);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static bool LogSqlServerMigrationPosture(
        ILogger logger,
        string provider,
        bool hasMigrationHistory)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!provider.Trim().Equals(
                DataAccessOptions.SqlServerProvider,
                StringComparison.OrdinalIgnoreCase)
            || hasMigrationHistory)
        {
            return false;
        }

        LogSqlServerMigrationHistoryMissing(logger);

        return true;
    }

    [LoggerMessage(
        EventId = 19100,
        Level = LogLevel.Information,
        Message = "Data access configured. Provider: {Provider}; ConnectionStringName: {ConnectionStringName}; EF Core auditing: {AuditingStatus}; AuditStorageMode: {AuditStorageMode}.")]
    private static partial void LogDataAccessConfiguration(
        ILogger logger,
        string provider,
        string connectionStringName,
        string auditingStatus,
        string auditStorageMode);

    [LoggerMessage(
        EventId = 19101,
        Level = LogLevel.Information,
        Message = "Application data access disabled. Provider: {Provider}; EF Core services were not registered.")]
    private static partial void LogDataAccessDisabled(
        ILogger logger,
        string provider);

    [LoggerMessage(
        EventId = 19102,
        Level = LogLevel.Warning,
        Message = "SQL Server is configured, but no EF Core migration history was discovered in the application assembly. The template intentionally omits the SQLite migration history from --dbProvider sqlserver scaffolds. Generate and review an initial SQL Server migration before using database-dependent application features. From the repository root run: dotnet ef migrations add InitialSqlServer --project src/ProjectTemplate.Infrastructure --startup-project src/ProjectTemplate.Web --context ApplicationDbContext --output-dir Data/Migrations. Startup will continue; NCAT does not auto-create or auto-apply migrations.")]
    private static partial void LogSqlServerMigrationHistoryMissing(
        ILogger logger);
}
