using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectTemplate.Web.Diagnostics;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

public sealed class ForwardedHeadersTrustDiagnosticsTests
{
    [Fact]
    public async Task StartAsync_MissingTrustBoundaryOutsideDevelopment_LogsWarning()
    {
        RecordingLogger<ForwardedHeadersTrustDiagnosticsHostedService> logger = new();
        ForwardedHeadersTrustDiagnosticsHostedService service = CreateService(
            Environments.Production,
            new ApplicationForwardedHeadersOptions
            {
                RequireExplicitProxyTrust = false
            },
            new ApplicationRateLimitingOptions(),
            logger);

        await service.StartAsync(CancellationToken.None);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(5101, entry.EventId.Id);
        Assert.Contains("one rate-limit bucket", entry.Message, StringComparison.Ordinal);
        Assert.Contains("KnownProxies or KnownNetworks", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_StrictModeWithoutTrustBoundary_Throws()
    {
        ForwardedHeadersTrustDiagnosticsHostedService service = CreateService(
            Environments.Production,
            new ApplicationForwardedHeadersOptions(),
            new ApplicationRateLimitingOptions(),
            new RecordingLogger<ForwardedHeadersTrustDiagnosticsHostedService>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));

        Assert.Contains("no trusted proxy or network is configured", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_DevelopmentDefaults_DoNotWarnOrFail()
    {
        RecordingLogger<ForwardedHeadersTrustDiagnosticsHostedService> logger = new();
        ForwardedHeadersTrustDiagnosticsHostedService service = CreateService(
            Environments.Development,
            new ApplicationForwardedHeadersOptions
            {
                RequireExplicitProxyTrust = true
            },
            new ApplicationRateLimitingOptions(),
            logger);

        await service.StartAsync(CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task StartAsync_ConfiguredKnownProxy_DoesNotWarn()
    {
        RecordingLogger<ForwardedHeadersTrustDiagnosticsHostedService> logger = new();
        ForwardedHeadersTrustDiagnosticsHostedService service = CreateService(
            Environments.Production,
            new ApplicationForwardedHeadersOptions
            {
                RequireExplicitProxyTrust = true,
                KnownProxies = ["203.0.113.10"]
            },
            new ApplicationRateLimitingOptions(),
            logger);

        await service.StartAsync(CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    private static ForwardedHeadersTrustDiagnosticsHostedService CreateService(
        string environmentName,
        ApplicationForwardedHeadersOptions forwardedHeadersOptions,
        ApplicationRateLimitingOptions rateLimitingOptions,
        ILogger<ForwardedHeadersTrustDiagnosticsHostedService> logger)
    {
        return new ForwardedHeadersTrustDiagnosticsHostedService(
            Microsoft.Extensions.Options.Options.Create(forwardedHeadersOptions),
            Microsoft.Extensions.Options.Options.Create(rateLimitingOptions),
            new TestHostEnvironment(environmentName),
            logger);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
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
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
