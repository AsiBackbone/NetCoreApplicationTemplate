using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Web.Tests;

public sealed class ApplicationAuditValueProtectorTests
{
    [Fact]
    public void TryProtect_OmitDisposition_ReturnsFalseAndEmptyProtectedValue()
    {
        bool included = ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(new(ApplicationAuditValueDisposition.Omit)),
            typeof(string),
            "SensitiveField",
            "secret",
            out object protectedValue);

        Assert.False(included);
        Assert.Equal(string.Empty, protectedValue);
    }

    [Fact]
    public void TryProtect_TruncateDisposition_BoundaryOnSurrogatePair_DoesNotWriteLoneSurrogate()
    {
        bool included = ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(new(ApplicationAuditValueDisposition.Truncate, MaximumLength: 2)),
            typeof(string),
            "DisplayName",
            "A😀B",
            out object protectedValue);

        Assert.True(included);
        Assert.Equal("A", protectedValue);
    }

    [Fact]
    public void TryProtect_TruncateDisposition_WithoutMaximumLength_ThrowsInvalidOperationException()
    {
        _ = Assert.Throws<InvalidOperationException>(() => ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(new(ApplicationAuditValueDisposition.Truncate)),
            typeof(string),
            "DisplayName",
            "abcdef",
            out _));
    }

    [Fact]
    public void TryProtect_HashDisposition_WithNullValue_HashesEmptyString()
    {
        bool included = ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(new(ApplicationAuditValueDisposition.Hash)),
            typeof(string),
            "Identifier",
            null,
            out object protectedValue);

        Assert.True(included);
        Assert.Equal(
            "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            protectedValue);
    }

    [Fact]
    public void TryProtect_HmacSha256Disposition_WithNullValue_HashesEmptyStringUsingConfiguredKey()
    {
        bool included = ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(ApplicationAuditValueDecision.HmacSha256("audit-key-1")),
            typeof(string),
            "Identifier",
            null,
            out object protectedValue);

        Assert.True(included);
        Assert.Equal(
            "EB468B318AD1D8EDA232AF4B526777E116A7A2ABE8553121DD692B450B19900A",
            protectedValue);
    }

    [Fact]
    public void TryProtect_HmacSha256Disposition_WithoutKey_ThrowsArgumentException()
    {
        _ = Assert.Throws<ArgumentException>(() => ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(new(ApplicationAuditValueDisposition.HmacSha256)),
            typeof(string),
            "Identifier",
            "value",
            out _));
    }

    [Fact]
    public void TryProtect_UnsupportedDisposition_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ApplicationAuditValueProtector.TryProtect(
                new FixedDecisionPolicy(new((ApplicationAuditValueDisposition)999)),
                typeof(string),
                "Field",
                "value",
                out _));

        Assert.Contains("Unsupported audit value disposition", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FixedDecisionPolicy(ApplicationAuditValueDecision decision) : IApplicationAuditValuePolicy
    {
        public ApplicationAuditValueDecision Evaluate(Type entityType, string propertyName, object? value)
        {
            ArgumentNullException.ThrowIfNull(entityType);
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            return decision;
        }
    }
}
