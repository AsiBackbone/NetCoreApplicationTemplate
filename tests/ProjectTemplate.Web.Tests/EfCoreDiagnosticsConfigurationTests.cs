using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Options;
using ProjectTemplate.Web.Extensions;

namespace ProjectTemplate.Web.Tests;

public sealed class EfCoreDiagnosticsConfigurationTests
{
    [Fact]
    public void AddApplicationDataAccess_DefaultDiagnostics_AreDisabled()
    {
        RecordingLoggerProvider loggerProvider = new();
        IConfiguration configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDatabase"] = "Data Source=:memory:"
            });

        using ServiceProvider serviceProvider = CreateServiceProvider(configuration, loggerProvider);
        using ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        DataAccessOptions options = serviceProvider
            .GetRequiredService<IOptions<DataAccessOptions>>()
            .Value;

        _ = context.Model;

        Assert.False(options.Diagnostics.EnableDetailedErrors);
        Assert.False(options.Diagnostics.EnableEfCoreTraceBridge);
        Assert.False(GetCoreOptionBoolean(context, "DetailedErrorsEnabled", "IsDetailedErrorsEnabled"));
        Assert.False(GetCoreOptionBoolean(context, "IsSensitiveDataLoggingEnabled", "SensitiveDataLoggingEnabled"));
        Assert.DoesNotContain(loggerProvider.Entries, entry => entry.EventId == 19000);
        Assert.NotNull(serviceProvider.GetRequiredService<ApplicationSaveChangesInterceptor>());
    }

    [Fact]
    public void AddApplicationDataAccess_EnabledDiagnostics_ApplyToScopedAndFactoryContexts()
    {
        RecordingLoggerProvider loggerProvider = new();
        IConfiguration configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDatabase"] = "Data Source=:memory:",
                ["ProjectTemplate:DataAccess:Diagnostics:EnableDetailedErrors"] = "true",
                ["ProjectTemplate:DataAccess:Diagnostics:EnableEfCoreTraceBridge"] = "true"
            });

        using ServiceProvider serviceProvider = CreateServiceProvider(configuration, loggerProvider);
        using IServiceScope scope = serviceProvider.CreateScope();
        using ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        DataAccessOptions options = scope.ServiceProvider
            .GetRequiredService<IOptions<DataAccessOptions>>()
            .Value;

        _ = context.Model;

        Assert.True(options.Diagnostics.EnableDetailedErrors);
        Assert.True(options.Diagnostics.EnableEfCoreTraceBridge);
        Assert.True(GetCoreOptionBoolean(context, "DetailedErrorsEnabled", "IsDetailedErrorsEnabled"));
        Assert.False(GetCoreOptionBoolean(context, "IsSensitiveDataLoggingEnabled", "SensitiveDataLoggingEnabled"));
        Assert.Contains(loggerProvider.Entries, entry => entry.EventId == 19000);

        int traceBridgeEntryCount = loggerProvider.Entries.Count(entry => entry.EventId == 19000);

        IDbContextFactory<ApplicationDbContext> factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        using ApplicationDbContext factoryContext = factory.CreateDbContext();
        _ = factoryContext.Model;

        Assert.True(GetCoreOptionBoolean(factoryContext, "DetailedErrorsEnabled", "IsDetailedErrorsEnabled"));
        Assert.False(GetCoreOptionBoolean(factoryContext, "IsSensitiveDataLoggingEnabled", "SensitiveDataLoggingEnabled"));
        Assert.True(loggerProvider.Entries.Count(entry => entry.EventId == 19000) > traceBridgeEntryCount);
    }

    private static ServiceProvider CreateServiceProvider(
        IConfiguration configuration,
        RecordingLoggerProvider loggerProvider)
    {
        ServiceCollection services = new();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(loggerProvider);
        });

        services.AddApplicationDataAccess(configuration);

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private static IConfiguration CreateConfiguration(
        IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static bool GetCoreOptionBoolean(
        ApplicationDbContext context,
        params string[] propertyNames)
    {
        IDbContextOptions dbContextOptions = context.GetService<IDbContextOptions>();
        object coreOptions = Assert.Single(
            dbContextOptions.Extensions,
            extension => extension.GetType().Name == "CoreOptionsExtension");

        PropertyInfo? property = propertyNames
            .Select(name => coreOptions.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public))
            .FirstOrDefault(candidate => candidate is not null);

        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(coreOptions));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(categoryName, _entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(
        string categoryName,
        ConcurrentQueue<LogEntry> entries) : ILogger
    {
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
            entries.Enqueue(new LogEntry(
                categoryName,
                eventId.Id,
                logLevel,
                formatter(state, exception)));
        }
    }

    private sealed record LogEntry(
        string CategoryName,
        int EventId,
        LogLevel Level,
        string Message);
}
