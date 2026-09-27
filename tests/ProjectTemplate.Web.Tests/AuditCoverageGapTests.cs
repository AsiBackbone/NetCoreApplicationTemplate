using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Extensions;
using ProjectTemplate.Infrastructure.Data.Options;
using ProjectTemplate.Web.HealthChecks;
using ProjectTemplate.Web.Services;

namespace ProjectTemplate.Web.Tests;

public sealed class AuditCoverageGapTests
{
    [Fact]
    public void AuditEntry_UsesExplicitApplicationName()
    {
        using var context = new AuditEntryTestContext();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry = context.Add(new AuditEntryTestEntity());
        var auditEntry = new AuditEntry(entry)
        {
            Application = "coverage-tests",
            TableName = "AuditEntryTestEntity",
            State = "Added"
        };

        var record = auditEntry.ToAuditRecord();

        Assert.Equal("coverage-tests", record.Application);
    }

    [Fact]
    public void MutationReceiptAccessor_RejectsNullDependencies()
    {
        var registry = new StubReceiptRegistry();

        _ = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationDbContextMutationAuditReceiptAccessor(null!, registry));

        using ApplicationDbContext context = CreateApplicationContext();
        _ = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationDbContextMutationAuditReceiptAccessor(context, null!));
    }

    [Fact]
    public void ReconciliationMetrics_ObservesMissingAndPresentOldestAge()
    {
        var observations = new List<double>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Name == "ncat.audit.outbox.oldest_pending_age_seconds")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => observations.Add(value));
        listener.Start();
        using var metrics = new ApplicationAuditReconciliationMetrics();

        metrics.UpdateDelivery(new(true, 0, null, 0, 0));
        listener.RecordObservableInstruments();
        metrics.UpdateDelivery(new(true, 1, TimeSpan.FromSeconds(12), 2, 0));
        listener.RecordObservableInstruments();

        Assert.Equal([0d, 12d], observations);
    }

    [Fact]
    public void AuditServiceRegistrations_ExposeCoreAndHostedServices()
    {
        ServiceCollection services = new();

        services.AddApplicationAuditedTransactions();
        services.AddApplicationAuditCompletionOutboxCore();
        services.AddApplicationAuditCompletionOutboxCore(options => options.BatchSize = 7);
        Web.Extensions.ApplicationAuditCompletionOutboxServiceExtensions
            .AddApplicationAuditCompletionOutbox(services, options => options.BatchSize = 9);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IApplicationAuditedTransaction));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
            descriptor.ImplementationType == typeof(ApplicationAuditCompletionOutboxHostedService));
        Assert.Equal(9, provider.GetRequiredService<IOptions<ApplicationAuditCompletionOutboxOptions>>().Value.BatchSize);
    }

    [Fact]
    public void DatabaseHealthCheck_RejectsNullDependencies()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        IOptionsMonitor<Options.ApplicationHealthCheckOptions> options =
            new StaticOptionsMonitor<Options.ApplicationHealthCheckOptions>(new());

        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationDatabaseHealthCheck(null!, options));
        _ = Assert.Throws<ArgumentNullException>(() => new ApplicationDatabaseHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            null!));
    }

    [Fact]
    public void AuditServiceRegistrations_RejectNullCollections()
    {
        IServiceCollection services = null!;

        _ = Assert.Throws<ArgumentNullException>(() => _ = services.AddApplicationAuditedTransactions());
        _ = Assert.Throws<ArgumentNullException>(() => _ = services.AddApplicationAuditCompletionOutboxCore());
        _ = Assert.Throws<ArgumentNullException>(() =>
            Web.Extensions.ApplicationAuditCompletionOutboxServiceExtensions
                .AddApplicationAuditCompletionOutbox(services));
    }

    private static ApplicationDbContext CreateApplicationContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var pipeline = new ApplicationSaveChangesPipeline(
            new TestCurrentActorAccessor(),
            Microsoft.Extensions.Options.Options.Create(new DataAccessOptions
            {
                Auditing = new DataAuditingOptions { Enabled = false }
            }));
        return new ApplicationDbContext(
            options,
            NullLogger<ApplicationDbContext>.Instance,
            new ApplicationSaveChangesInterceptor(pipeline));
    }

    private sealed class AuditEntryTestContext : DbContext
    {
        public DbSet<AuditEntryTestEntity> Entities => Set<AuditEntryTestEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            _ = optionsBuilder.UseSqlite("Data Source=:memory:");
        }
    }

    private sealed class AuditEntryTestEntity
    {
        public int Id { get; set; }
    }

    private sealed class StubReceiptRegistry : IApplicationMutationAuditReceiptRegistry
    {
        public ApplicationMutationAuditReceipt? GetLastCompletedReceipt(ApplicationDbContext dbContext)
        {
            return null;
        }
    }

    private sealed class TestCurrentActorAccessor : ICurrentActorAccessor
    {
        public string CurrentActor => "Coverage gap test";
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name)
        {
            return value;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
