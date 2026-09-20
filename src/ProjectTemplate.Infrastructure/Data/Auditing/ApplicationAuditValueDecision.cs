namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Describes how an audited property value should be represented.
/// </summary>
public sealed record ApplicationAuditValueDecision(
    ApplicationAuditValueDisposition Disposition,
    int? MaximumLength = null)
{
    /// <summary>
    /// Gets an optional UTF-8 key used when <see cref="Disposition" /> is <see cref="ApplicationAuditValueDisposition.HmacSha256" />.
    /// </summary>
    public string? HmacSha256Key { get; init; }

    public static ApplicationAuditValueDecision Include { get; } = new(ApplicationAuditValueDisposition.Include);

    /// <summary>
    /// Gets a decision that records the property as changed while replacing its value with a fixed mask.
    /// </summary>
    public static ApplicationAuditValueDecision Mask { get; } = new(ApplicationAuditValueDisposition.Mask);

    /// <summary>
    /// Creates a decision that records an HMAC-SHA-256 digest using the supplied key.
    /// </summary>
    public static ApplicationAuditValueDecision HmacSha256(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new(ApplicationAuditValueDisposition.HmacSha256)
        {
            HmacSha256Key = key
        };
    }
}
