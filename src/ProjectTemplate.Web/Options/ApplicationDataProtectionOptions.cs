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
}
