namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Defines how an application audit value should be represented.
/// </summary>
public enum ApplicationAuditValueDisposition
{
    /// <summary>
    /// Records the value unchanged.
    /// </summary>
    Include = 0,

    /// <summary>
    /// Records a fixed mask token instead of the value.
    /// </summary>
    Mask = 1,

    /// <summary>
    /// Records an unsalted SHA-256 hash of the value.
    /// This does not protect low-entropy values such as email addresses because
    /// an attacker holding the audit table can brute-force candidate values.
    /// Use this only for high-entropy values where correlation matters more than secrecy.
    /// </summary>
    Hash = 2,

    /// <summary>
    /// Omits the value and records only that the property changed.
    /// </summary>
    Omit = 3,

    /// <summary>
    /// Records only the first segment of the value.
    /// </summary>
    Truncate = 4,

    /// <summary>
    /// Records an HMAC-SHA-256 hash of the value using a configured key.
    /// This resists offline brute-force against low-entropy values better than <see cref="Hash" />.
    /// </summary>
    HmacSha256 = 5
}
