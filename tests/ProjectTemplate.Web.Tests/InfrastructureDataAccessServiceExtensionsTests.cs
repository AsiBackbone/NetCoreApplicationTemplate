using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Extensions;
using ProjectTemplate.Infrastructure.Data.ExternalLogins;

namespace ProjectTemplate.Web.Tests;

public sealed class InfrastructureDataAccessServiceExtensionsTests
{
    [Fact]
    public void AddApplicationInfrastructureDataAccess_NonWebContainer_ResolvesDbContextFactoryAndPipeline()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ApplicationDatabase"] = "Data Source=:memory:",
                    ["ProjectTemplate:DataAccess:Provider"] = "Sqlite",
                    ["ProjectTemplate:DataAccess:ConnectionStringName"] = "ApplicationDatabase"
                })
            .Build();

        ServiceCollection services = new();

        services.AddLogging();
        services.AddApplicationInfrastructureDataAccess(configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using IServiceScope scope = serviceProvider.CreateScope();

        ICurrentActorAccessor actorAccessor = scope.ServiceProvider
            .GetRequiredService<ICurrentActorAccessor>();

        IApplicationAuditStore auditStore = scope.ServiceProvider
            .GetRequiredService<IApplicationAuditStore>();

        IApplicationSaveChangesPipeline saveChangesPipeline = scope.ServiceProvider
            .GetRequiredService<IApplicationSaveChangesPipeline>();

        IApplicationMutationAuditReceiptRegistry receiptRegistry = scope.ServiceProvider
            .GetRequiredService<IApplicationMutationAuditReceiptRegistry>();

        ApplicationSaveChangesInterceptor saveChangesInterceptor = scope.ServiceProvider
            .GetRequiredService<ApplicationSaveChangesInterceptor>();

        using ApplicationDbContext context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        IApplicationMutationAuditReceiptAccessor receiptAccessor = scope.ServiceProvider
            .GetRequiredService<IApplicationMutationAuditReceiptAccessor>();

        IDbContextFactory<ApplicationDbContext> dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        using ApplicationDbContext factoryContext = dbContextFactory.CreateDbContext();

        Assert.Equal(SystemCurrentActorAccessor.ActorName, actorAccessor.CurrentActor);
        Assert.IsType<LocalApplicationAuditStore>(auditStore);
        Assert.NotNull(saveChangesPipeline);
        Assert.Same(saveChangesPipeline, receiptRegistry);
        Assert.Null(receiptAccessor.LastCompletedReceipt);
        Assert.NotNull(saveChangesInterceptor);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", factoryContext.Database.ProviderName);
    }

    [Fact]
    public void ApplicationDbContext_MissingSaveChangesPipelineRegistration_ThrowsDuringResolution()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddScoped<ApplicationSaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseSqlite("Data Source=:memory:"));

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>);

        Assert.Contains(
            nameof(IApplicationSaveChangesPipeline),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddApplicationInfrastructureDataAccess_DisabledProvider_SkipsDbContextFactoryAndEfBackedServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ProjectTemplate:DataAccess:Provider"] = "None"
                })
            .Build();

        ServiceCollection services = new();

        services.AddLogging();
        services.AddApplicationInfrastructureDataAccess(configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.Null(serviceProvider.GetService<ICurrentActorAccessor>());
        Assert.Null(serviceProvider.GetService<IApplicationAuditStore>());
        Assert.Null(serviceProvider.GetService<IApplicationSaveChangesPipeline>());
        Assert.Null(serviceProvider.GetService<IApplicationMutationAuditReceiptRegistry>());
        Assert.Null(serviceProvider.GetService<IApplicationMutationAuditReceiptAccessor>());
        Assert.Null(serviceProvider.GetService<ApplicationSaveChangesInterceptor>());
        Assert.Null(serviceProvider.GetService<ApplicationDbContext>());
        Assert.Null(serviceProvider.GetService<IDbContextFactory<ApplicationDbContext>>());
        Assert.Null(serviceProvider.GetService<IExternalLoginAccountResolver>());
    }
}
