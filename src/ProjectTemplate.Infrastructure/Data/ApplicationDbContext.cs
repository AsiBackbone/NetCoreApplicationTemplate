using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Infrastructure.Data;

/// <summary>
/// Represents the EF Core database context for the ProjectTemplate application.
/// </summary>
public sealed partial class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ILogger<ApplicationDbContext> logger,
    IApplicationSaveChangesPipeline saveChangesPipeline,
    ApplicationSaveChangesInterceptor? saveChangesInterceptor = null
)
    : DbContext(options)
{
    private readonly ILogger<ApplicationDbContext> _logger = logger;
    private readonly IApplicationSaveChangesPipeline _saveChangesPipeline =
        saveChangesPipeline ?? throw new ArgumentNullException(nameof(saveChangesPipeline));
    private readonly ApplicationSaveChangesInterceptor? _configuredSaveChangesInterceptor = saveChangesInterceptor;

    /// <summary>
    /// Gets the audit records for the application.
    /// </summary>
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    /// <summary>
    /// Gets the durable, minimized audit-completion outbox entries.
    /// </summary>
    public DbSet<ApplicationAuditCompletionOutboxEntry> ApplicationAuditCompletionOutboxEntries =>
        Set<ApplicationAuditCompletionOutboxEntry>();

    /// <summary>
    /// Gets the external login account links for the application.
    /// </summary>
    public DbSet<ExternalLoginAccount> ExternalLoginAccounts => Set<ExternalLoginAccount>();

    [LoggerMessage(
        EventId = 19001,
        Level = LogLevel.Warning,
        Message = "Optimistic concurrency conflict detected while saving {EntryCount} tracked entity entries.")]
    private static partial void LogOptimisticConcurrencyConflict(
        ILogger logger,
        int entryCount,
        Exception exception);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ConfigureDataEntityDefaults(modelBuilder);
        ConfigureTimestampDefaults(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        ApplicationSaveChangesInterceptor interceptor =
            _configuredSaveChangesInterceptor ?? new ApplicationSaveChangesInterceptor(_saveChangesPipeline);

        _ = optionsBuilder.AddInterceptors(interceptor);

        base.OnConfiguring(optionsBuilder);
    }

    public bool HasUnsavedChanges()
    {
        return ChangeTracker.HasChanges();
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess = true)
    {
        return SaveChangesWithConcurrencyHandling(
            () => base.SaveChanges(acceptAllChangesOnSuccess));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        return SaveChangesWithConcurrencyHandlingAsync(
            () => base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken));
    }

    private static bool IsUtcTimestampProperty(string propertyName)
    {
        return propertyName.EndsWith("Utc", StringComparison.Ordinal);
    }

    private static void ConfigureDataEntityDefaults(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(DataEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            _ = modelBuilder.Entity(entityType.ClrType)
                .Property<string>(nameof(DataEntity.ConcurrencyStamp))
                .HasMaxLength(64)
                .IsRequired()
                .IsConcurrencyToken();
        }
    }

    private static void ConfigureTimestampDefaults(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                Type propertyType = Nullable.GetUnderlyingType(property.ClrType)
                    ?? property.ClrType;

                if ((propertyType == typeof(DateTime) || propertyType == typeof(DateTimeOffset)) &&
                    IsUtcTimestampProperty(property.Name))
                {
                    property.SetPrecision(PersistenceTimestamp.Precision);
                }
            }
        }
    }

    private int SaveChangesWithConcurrencyHandling(Func<int> saveChanges)
    {
        try
        {
            return saveChanges();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            LogOptimisticConcurrencyConflict(_logger, exception.Entries.Count, exception);
            throw;
        }
    }

    private async Task<int> SaveChangesWithConcurrencyHandlingAsync(Func<Task<int>> saveChanges)
    {
        try
        {
            return await saveChanges().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            LogOptimisticConcurrencyConflict(_logger, exception.Entries.Count, exception);
            throw;
        }
    }
}
