using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProjectTemplate.Infrastructure.Data.Auditing;

internal static class ApplicationAuditValueProtector
{
    private const string _maskedValue = "***";

    internal static bool TryProtect(
        IApplicationAuditValuePolicy policy,
        Type entityType,
        string propertyName,
        object? value,
        out object protectedValue)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        ApplicationAuditValueDecision decision = policy.Evaluate(entityType, propertyName, value)
            ?? throw new InvalidOperationException("The application audit value policy returned no decision.");

        switch (decision.Disposition)
        {
            case ApplicationAuditValueDisposition.Include:
                protectedValue = value ?? string.Empty;
                return true;
            case ApplicationAuditValueDisposition.Mask:
                protectedValue = _maskedValue;
                return true;
            case ApplicationAuditValueDisposition.Hash:
                protectedValue = Hash(value);
                return true;
            case ApplicationAuditValueDisposition.Omit:
                protectedValue = string.Empty;
                return false;
            case ApplicationAuditValueDisposition.Truncate:
                protectedValue = Truncate(value, decision.MaximumLength);
                return true;
            default:
                throw new InvalidOperationException($"Unsupported audit value disposition '{decision.Disposition}'.");
        }
    }

    private static string Hash(object? value)
    {
        string canonicalValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalValue));
        return Convert.ToHexString(hash);
    }

    private static string Truncate(object? value, int? maximumLength)
    {
        if (maximumLength is null or <= 0)
        {
            throw new InvalidOperationException("Truncated audit values require a positive maximum length.");
        }

        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length <= maximumLength.Value)
        {
            return text;
        }

        int truncationLength = maximumLength.Value;
        if (char.IsHighSurrogate(text[truncationLength - 1])
            && char.IsLowSurrogate(text[truncationLength]))
        {
            truncationLength--;
        }

        return text[..truncationLength];
    }
}
