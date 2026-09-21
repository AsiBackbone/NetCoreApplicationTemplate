namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Defines how an application audit value should be represented.
/// </summary>
public enum ApplicationAuditValueDisposition
{
    /// <summary>
    /// Records the value unchanged, except null is normalized to <see cref="string.Empty" />.
    /// </summary>
    Include = 0,

    /// <summary>
    /// Records a fixed mask token instead of the value.
    /// </summary>
    Mask = 1,

    /// <summary>
    /// Records an unsalted, unkeyed SHA-256 hash of the value for integrity and change detection only.
    /// This is not a confidentiality control: low-entropy values such as email addresses, phone numbers,
    /// national identifiers, booleans, enum names, and small numbers are trivially recovered by a
    /// dictionary attack against the audit table. Use <see cref="HmacSha256" /> when the value must stay
    /// confidential, and use this only for high-entropy values where correlation matters more than secrecy.
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
