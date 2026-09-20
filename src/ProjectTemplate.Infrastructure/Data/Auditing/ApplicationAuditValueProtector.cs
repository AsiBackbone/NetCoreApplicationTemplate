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
            case ApplicationAuditValueDisposition.HmacSha256:
                protectedValue = HmacSha256(value, decision.HmacSha256Key);
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
        byte[] canonicalValue = Encoding.UTF8.GetBytes(ToCanonicalString(value));
        byte[] hash = SHA256.HashData(canonicalValue);
        return Convert.ToHexString(hash);
    }

    private static string HmacSha256(object? value, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("HMAC-SHA-256 audit values require a non-empty key.", nameof(key));
        }

        byte[] canonicalValue = Encoding.UTF8.GetBytes(ToCanonicalString(value));
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] hash = HMACSHA256.HashData(keyBytes, canonicalValue);
        return Convert.ToHexString(hash);
    }

    private static string ToCanonicalString(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Truncate(object? value, int? maximumLength)
    {
        if (maximumLength is null or <= 0)
        {
            throw new InvalidOperationException("Truncated audit values require a positive maximum length.");
        }

        string text = ToCanonicalString(value);
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
