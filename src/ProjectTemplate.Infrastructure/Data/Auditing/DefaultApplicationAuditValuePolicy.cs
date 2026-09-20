using ProjectTemplate.Infrastructure.Data.Entities;

namespace ProjectTemplate.Infrastructure.Data.Auditing;

/// <summary>
/// Includes audited values unchanged except for the personal fields the template ships, which are masked.
/// </summary>
/// <remarks>
/// <para>
/// Audit records are durable and retained, so a value written here outlives the row it describes. This policy masks
/// the contact fields on <see cref="ExternalLoginAccount" /> because a generated application would otherwise persist
/// an end user's email address and display name into audit history the first time an external login is created or
/// updated, without the consumer having chosen that. Masking still records that the property changed, so the audit
/// trail keeps its accountability value without carrying the value itself.
/// </para>
/// <para>
/// Every other property is included unchanged. Applications that add entities carrying personal data should replace
/// this policy rather than extend it, since the fields worth protecting depend on the application's own model. See
/// the audit accountability documentation for the replacement seam.
/// </para>
/// </remarks>
public sealed class DefaultApplicationAuditValuePolicy : IApplicationAuditValuePolicy
{
    public ApplicationAuditValueDecision Evaluate(
        Type entityType,
        string propertyName,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return IsMaskedExternalLoginAccountProperty(entityType, propertyName)
            ? ApplicationAuditValueDecision.Mask
            : ApplicationAuditValueDecision.Include;
    }

    private static bool IsMaskedExternalLoginAccountProperty(Type entityType, string propertyName)
    {
        return entityType == typeof(ExternalLoginAccount)
            && propertyName is nameof(ExternalLoginAccount.Email)
                or nameof(ExternalLoginAccount.NormalizedEmail)
                or nameof(ExternalLoginAccount.DisplayName);
    }
}
