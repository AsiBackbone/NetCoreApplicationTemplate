using Microsoft.AspNetCore.DataProtection;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods for configuring the persistent ASP.NET Core Data Protection key ring.
/// </summary>
public static class DataProtectionServiceExtensions
{
    /// <summary>
    /// Registers Data Protection with a stable application discriminator and persistent filesystem key ring.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <returns>The same service collection instance for chaining.</returns>
    public static IServiceCollection AddApplicationDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        IConfigurationSection section = configuration.GetSection(ApplicationDataProtectionOptions.SectionName);

        services
            .AddOptions<ApplicationDataProtectionOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApplicationName),
                "ProjectTemplate:DataProtection:ApplicationName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.KeyRingPath),
                "ProjectTemplate:DataProtection:KeyRingPath is required.")
            .ValidateOnStart();

        ApplicationDataProtectionOptions options = section.Get<ApplicationDataProtectionOptions>() ?? new();

        string applicationName = !string.IsNullOrWhiteSpace(options.ApplicationName)
            ? options.ApplicationName.Trim()
            : throw new InvalidOperationException("ProjectTemplate:DataProtection:ApplicationName is required.");
        string configuredKeyRingPath = !string.IsNullOrWhiteSpace(options.KeyRingPath)
            ? options.KeyRingPath.Trim()
            : throw new InvalidOperationException("ProjectTemplate:DataProtection:KeyRingPath is required.");
        string keyRingPath = Path.IsPathFullyQualified(configuredKeyRingPath)
            ? configuredKeyRingPath
            : Path.GetFullPath(configuredKeyRingPath, environment.ContentRootPath);

        services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

        return services;
    }
}
