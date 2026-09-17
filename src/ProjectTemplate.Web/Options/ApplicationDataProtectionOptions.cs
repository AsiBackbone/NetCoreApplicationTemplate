namespace ProjectTemplate.Web.Options;

/// <summary>
/// Options controlling the persistent ASP.NET Core Data Protection key ring.
/// </summary>
public sealed class ApplicationDataProtectionOptions
{
    /// <summary>
    /// Configuration section name for Data Protection settings.
    /// </summary>
    public const string SectionName = "ProjectTemplate:DataProtection";

    /// <summary>
    /// Gets or sets the discriminator shared by application instances that must read the same protected payloads.
    /// </summary>
    public string ApplicationName { get; set; } = "ProjectTemplate.Web";

    /// <summary>
    /// Gets or sets the persistent key-ring directory. Relative paths are resolved from the content root.
    /// </summary>
    public string KeyRingPath { get; set; } = "DataProtection-Keys";

    /// <summary>
    /// Gets or sets the path to a PKCS#12 (<c>.pfx</c>) certificate used to encrypt key-ring files at rest.
    /// Relative paths are resolved from the content root.
    /// </summary>
    /// <remarks>
    /// When not set, key-ring files are written without application-level encryption. On Windows the framework
    /// protects them with DPAPI for the current user; on Linux and macOS they are written in plain text. The certificate
    /// must include its private key, and every replica must load the same certificate.
    /// </remarks>
    public string? KeyEncryptionCertificatePath { get; set; }

    /// <summary>
    /// Gets or sets the password for <see cref="KeyEncryptionCertificatePath"/>.
    /// </summary>
    /// <remarks>
    /// Supply this value from a secret store, environment variable, or mounted secret. Do not commit it to
    /// <c>appsettings.json</c>.
    /// </remarks>
    public string? KeyEncryptionCertificatePassword { get; set; }
}
