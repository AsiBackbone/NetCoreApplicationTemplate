using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectTemplate.Web.Authentication.Options;
using ProjectTemplate.Web.Extensions;

namespace ProjectTemplate.Web.Tests;

public sealed class StartupSecurityPostureExtensionsTests
{
    [Fact]
    public void LogApplicationSecurityPosture_AuthenticationDisabled_EmitsWarning()
    {
        TestLogger logger = new();
        IConfiguration configuration = CreateConfiguration(authenticationEnabled: false);
        TestHostEnvironment environment = new(Environments.Development);

        StartupSecurityPostureExtensions.LogApplicationSecurityPosture(
            logger,
            configuration,
            environment);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(
            $"{ApplicationAuthenticationOptions.SectionName}:Enabled",
            entry.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "supported configuration",
            entry.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogApplicationSecurityPosture_AuthenticationEnabled_DoesNotEmitAuthenticationWarning()
    {
        TestLogger logger = new();
        IConfiguration configuration = CreateConfiguration(authenticationEnabled: true);
        TestHostEnvironment environment = new(Environments.Development);

        StartupSecurityPostureExtensions.LogApplicationSecurityPosture(
            logger,
            configuration,
            environment);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void LogApplicationSecurityPosture_Production_EmitsAnonymousHealthEndpointWarning()
    {
        TestLogger logger = new();
        IConfiguration configuration = CreateConfiguration(authenticationEnabled: true);
        TestHostEnvironment environment = new(Environments.Production);

        StartupSecurityPostureExtensions.LogApplicationSecurityPosture(
            logger,
            configuration,
            environment);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("/health", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/health/ready", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/health/live", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Production", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogApplicationSecurityPosture_Development_DoesNotEmitHealthEndpointWarning()
    {
        TestLogger logger = new();
        IConfiguration configuration = CreateConfiguration(authenticationEnabled: true);
        TestHostEnvironment environment = new(Environments.Development);

        StartupSecurityPostureExtensions.LogApplicationSecurityPosture(
            logger,
            configuration,
            environment);

        Assert.Empty(logger.Entries);
    }

    private static IConfiguration CreateConfiguration(bool authenticationEnabled)
    {
        Dictionary<string, string?> values = new()
        {
            [$"{ApplicationAuthenticationOptions.SectionName}:Enabled"] =
                authenticationEnabled.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
