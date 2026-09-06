namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Describes how an audited property value should be represented.
/// </summary>
public sealed record ApplicationAuditValueDecision(
    ApplicationAuditValueDisposition Disposition,
    int? MaximumLength = null)
{
    public static ApplicationAuditValueDecision Include { get; } = new(ApplicationAuditValueDisposition.Include);

    /// <summary>
    /// Gets a decision that records the property as changed while replacing its value with a fixed mask.
    /// </summary>
    public static ApplicationAuditValueDecision Mask { get; } = new(ApplicationAuditValueDisposition.Mask);
}
