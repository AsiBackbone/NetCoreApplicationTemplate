using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Extensions;

/// <summary>
/// Provides extension methods for configuring the persistent ASP.NET Core Data Protection key ring.
/// </summary>
public static class DataProtectionServiceExtensions
{
    private const string _keyEncryptionPasswordWithoutPathMessage =
        "ProjectTemplate:DataProtection:KeyEncryptionCertificatePassword requires KeyEncryptionCertificatePath.";

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
            .Validate(
                options => string.IsNullOrEmpty(options.KeyEncryptionCertificatePassword) ||
                    !string.IsNullOrWhiteSpace(options.KeyEncryptionCertificatePath),
                _keyEncryptionPasswordWithoutPathMessage)
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

        IDataProtectionBuilder dataProtectionBuilder = services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

        if (!string.IsNullOrWhiteSpace(options.KeyEncryptionCertificatePath))
        {
            X509Certificate2 keyEncryptionCertificate = LoadKeyEncryptionCertificate(
                options.KeyEncryptionCertificatePath.Trim(),
                options.KeyEncryptionCertificatePassword,
                environment.ContentRootPath);

            // ProtectKeysWithCertificate encrypts new keys. UnprotectKeysWithAnyCertificate supplies the same
            // certificate for decryption, which is required on Linux and macOS where the framework cannot resolve it
            // from a certificate store by thumbprint.
            _ = dataProtectionBuilder
                .ProtectKeysWithCertificate(keyEncryptionCertificate)
                .UnprotectKeysWithAnyCertificate(keyEncryptionCertificate);
        }
        else if (!string.IsNullOrEmpty(options.KeyEncryptionCertificatePassword))
        {
            throw new InvalidOperationException(_keyEncryptionPasswordWithoutPathMessage);
        }

        return services;
    }

    private static X509Certificate2 LoadKeyEncryptionCertificate(
        string configuredCertificatePath,
        string? password,
        string contentRootPath)
    {
        string certificatePath = Path.IsPathFullyQualified(configuredCertificatePath)
            ? configuredCertificatePath
            : Path.GetFullPath(configuredCertificatePath, contentRootPath);

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                $"ProjectTemplate:DataProtection:KeyEncryptionCertificatePath '{certificatePath}' was not found.");
        }

        // EphemeralKeySet avoids writing the private key to the user profile or machine key store. macOS does not
        // support ephemeral key sets, so the default storage is used there.
        X509KeyStorageFlags keyStorageFlags = OperatingSystem.IsMacOS()
            ? X509KeyStorageFlags.DefaultKeySet
            : X509KeyStorageFlags.EphemeralKeySet;

        X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            password,
            keyStorageFlags);

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "ProjectTemplate:DataProtection:KeyEncryptionCertificatePath must reference a certificate that includes its private key.");
        }

        return certificate;
    }
}
