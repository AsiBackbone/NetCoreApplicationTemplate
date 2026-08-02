using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.ExternalLogins;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Infrastructure.Data.Extensions;

/// <summary>
/// Provides infrastructure-owned service registration methods for application data access.
/// </summary>
public static class InfrastructureDataAccessServiceExtensions
{
    /// <summary>
    /// Registers EF Core data access services for infrastructure and non-web consumers.
    /// </summary>
    public static IServiceCollection AddApplicationInfrastructureDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<DataAccessOptions>()
            .Bind(configuration.GetSection(DataAccessOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider),
                "ProjectTemplate:DataAccess:Provider must not be empty.")
            .Validate(options => DataAccessOptions.IsDisabledProvider(options.Provider)
                || !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "ProjectTemplate:DataAccess:ConnectionStringName must not be empty when data access is enabled.")
            .Validate(options => options.Diagnostics is not null,
                "ProjectTemplate:DataAccess:Diagnostics must be configured as an object when specified.")
            .Validate(options => AuditStorageModes.IsSupported(options.Auditing.StorageMode),
                "ProjectTemplate:DataAccess:Auditing:StorageMode must be Local, Outbox, or ExternalSink.")
            .ValidateOnStart();

        DataAccessRegistration registration = ResolveDataAccessRegistration(configuration);
        if (registration.IsDisabled)
        {
            return services;
        }

        services.TryAddScoped<ICurrentActorAccessor, SystemCurrentActorAccessor>();
        services.TryAddScoped<IApplicationAuditContextAccessor, SystemApplicationAuditContextAccessor>();
        services.TryAddScoped<IApplicationAuditValuePolicy, DefaultApplicationAuditValuePolicy>();
        services.TryAddSingleton<IApplicationMutationManifestBuilder, CanonicalApplicationMutationManifestBuilder>();
        services.TryAddSingleton<IApplicationMutationManifestHasher, Sha256ApplicationMutationManifestHasher>();
        services.TryAddSingleton<IApplicationMutationManifestVerifier, ApplicationMutationManifestVerifier>();

        if (AuditStorageModes.IsLocal(registration.AuditStorageMode))
        {
            services.TryAddScoped<IApplicationAuditStore, LocalApplicationAuditStore>();
        }

        services.TryAddScoped<ContextIsolatedApplicationSaveChangesPipeline>();
        services.TryAddScoped<IApplicationSaveChangesPipeline>(serviceProvider =>
            serviceProvider.GetRequiredService<ContextIsolatedApplicationSaveChangesPipeline>());
        services.TryAddScoped<IApplicationMutationAuditReceiptRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<ContextIsolatedApplicationSaveChangesPipeline>());
        services.TryAddScoped<IApplicationMutationAuditReceiptAccessor, ApplicationDbContextMutationAuditReceiptAccessor>();
        services.TryAddScoped<ApplicationSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            ConfigureDataAccess(
                serviceProvider,
                options,
                registration));

        services.AddDbContextFactory<ApplicationDbContext>(
            (serviceProvider, options) => ConfigureDataAccess(
                serviceProvider,
                options,
                registration),
            ServiceLifetime.Scoped);

        services.TryAddScoped<IExternalLoginAccountResolver, EfCoreExternalLoginAccountResolver>();
        return services;
    }

    private static DataAccessRegistration ResolveDataAccessRegistration(IConfiguration configuration)
    {
        DataAccessOptions dataAccessOptions = configuration
            .GetSection(DataAccessOptions.SectionName)
            .Get<DataAccessOptions>() ?? new DataAccessOptions();

        string provider = dataAccessOptions.Provider?.Trim() ?? string.Empty;
        string connectionStringName = dataAccessOptions.ConnectionStringName?.Trim() ?? string.Empty;
        string auditStorageMode = AuditStorageModes.Normalize(dataAccessOptions.Auditing.StorageMode);

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException("Application data access provider was not configured.");
        }

        if (!AuditStorageModes.IsSupported(auditStorageMode))
        {
            throw new InvalidOperationException("Application audit storage mode was not configured with a supported value.");
        }

        if (DataAccessOptions.IsDisabledProvider(provider))
        {
            return DataAccessRegistration.Disabled(provider, auditStorageMode);
        }

        if (string.IsNullOrWhiteSpace(connectionStringName))
        {
            throw new InvalidOperationException("Application data access connection string name was not configured.");
        }

        string connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' was not configured.");

        return DataAccessRegistration.Enabled(
            provider,
            connectionString,
            auditStorageMode,
            dataAccessOptions.Diagnostics.EnableDetailedErrors,
            dataAccessOptions.Diagnostics.EnableEfCoreTraceBridge);
    }

    private static void ConfigureDataAccess(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        DataAccessRegistration registration)
    {
        ConfigureProvider(
            options,
            registration.Provider,
            registration.ConnectionString);

        if (registration.EnableDetailedErrors)
        {
            _ = options.EnableDetailedErrors();
        }

        if (registration.EnableEfCoreTraceBridge)
        {
            ILogger<ApplicationDbContext> logger = serviceProvider
                .GetRequiredService<ILogger<ApplicationDbContext>>();

            _ = options.LogTo(
                message => EfCoreDiagnosticsLogging.LogEfCoreMessage(logger, message),
                LogLevel.Trace);
        }
    }

    private static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string provider,
        string connectionString)
    {
        if (provider.Equals(DataAccessOptions.SqliteProvider, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlite(connectionString);
            return;
        }

        if (provider.Equals(DataAccessOptions.SqlServerProvider, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(connectionString);
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported data access provider '{provider}'. Supported providers: {DataAccessOptions.SqliteProvider}, {DataAccessOptions.SqlServerProvider}, {DataAccessOptions.DisabledProvider}.");
    }

    private readonly record struct DataAccessRegistration(
        string Provider,
        string ConnectionString,
        string AuditStorageMode,
        bool EnableDetailedErrors,
        bool EnableEfCoreTraceBridge,
        bool IsDisabled)
    {
        public static DataAccessRegistration Enabled(
            string provider,
            string connectionString,
            string auditStorageMode,
            bool enableDetailedErrors,
            bool enableEfCoreTraceBridge)
        {
            return new(
                provider,
                connectionString,
                auditStorageMode,
                enableDetailedErrors,
                enableEfCoreTraceBridge,
                false);
        }

        public static DataAccessRegistration Disabled(
            string provider,
            string auditStorageMode)
        {
            return new(
                provider,
                string.Empty,
                auditStorageMode,
                false,
                false,
                true);
        }
    }
}

internal static partial class EfCoreDiagnosticsLogging
{
    [LoggerMessage(
        EventId = 19000,
        Level = LogLevel.Trace,
        Message = "{EfCoreMessage}")]
    internal static partial void LogEfCoreMessage(
        ILogger logger,
        string efCoreMessage);
}
