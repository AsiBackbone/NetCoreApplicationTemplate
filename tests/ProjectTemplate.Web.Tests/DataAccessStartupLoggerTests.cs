using Microsoft.Extensions.Logging;
using ProjectTemplate.Infrastructure.Data.Options;
using ProjectTemplate.Infrastructure.Data.Services;

namespace ProjectTemplate.Web.Tests;

public sealed class DataAccessStartupLoggerTests
{
    [Fact]
    public void LogSqlServerMigrationPosture_SqlServerWithoutMigrations_EmitsActionableWarning()
    {
        TestLogger logger = new();

        bool warningEmitted = DataAccessStartupLogger.LogSqlServerMigrationPosture(
            logger,
            DataAccessOptions.SqlServerProvider,
            hasMigrationHistory: false);

        Assert.True(warningEmitted);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(
            "intentionally omits the SQLite migration history",
            entry.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet ef migrations add InitialSqlServer",
            entry.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "--project src/ProjectTemplate.Infrastructure",
            entry.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "--startup-project src/ProjectTemplate.Web",
            entry.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not auto-create or auto-apply migrations",
            entry.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SqlServer", true)]
    [InlineData("sqlserver", true)]
    [InlineData("Sqlite", false)]
    [InlineData("None", false)]
    [InlineData("Disabled", false)]
    public void LogSqlServerMigrationPosture_UnaffectedPostures_DoNotWarn(
        string provider,
        bool hasMigrationHistory)
    {
        TestLogger logger = new();

        bool warningEmitted = DataAccessStartupLogger.LogSqlServerMigrationPosture(
            logger,
            provider,
            hasMigrationHistory);

        Assert.False(warningEmitted);
        Assert.Empty(logger.Entries);
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
