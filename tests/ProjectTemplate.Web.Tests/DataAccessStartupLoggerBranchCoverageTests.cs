using Microsoft.Extensions.Logging;
using ProjectTemplate.Infrastructure.Data.Options;
using ProjectTemplate.Infrastructure.Data.Services;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the disabled-provider and auditing-status branches of <c>DataAccessStartupLogger.StartAsync</c>.
/// </summary>
public sealed class DataAccessStartupLoggerBranchCoverageTests
{
    [Theory]
    [InlineData("None")]
    [InlineData(" disabled ")]
    public async Task StartAsync_DisabledProvider_LogsOnlyTheDisabledPosture(string provider)
    {
        RecordingLogger logger = new();
        DataAccessStartupLogger startupLogger = new(
            logger,
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions { Provider = provider }));

        await startupLogger.StartAsync(TestContext.Current.CancellationToken);
        await startupLogger.StopAsync(TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("data access disabled", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "EF Core auditing: enabled")]
    [InlineData(false, "EF Core auditing: disabled")]
    public async Task StartAsync_SqliteProvider_LogsAuditingStatus(bool auditingEnabled, string expectedFragment)
    {
        RecordingLogger logger = new();
        DataAccessStartupLogger startupLogger = new(
            logger,
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Provider = DataAccessOptions.SqliteProvider,
                Auditing = new DataAuditingOptions { Enabled = auditingEnabled }
            }));

        await startupLogger.StartAsync(TestContext.Current.CancellationToken);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(expectedFragment, entry.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingLogger : ILogger<DataAccessStartupLogger>
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
