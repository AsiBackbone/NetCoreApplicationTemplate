using System.Globalization;
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

    [Theory]
    [InlineData(ApplicationAuditValueDisposition.Hash)]
    [InlineData(ApplicationAuditValueDisposition.HmacSha256)]
    public void TryProtect_DigestDispositions_DistinctByteArrays_ProduceDistinctDigests(
        ApplicationAuditValueDisposition disposition)
    {
        ApplicationAuditValueDecision decision = disposition == ApplicationAuditValueDisposition.HmacSha256
            ? ApplicationAuditValueDecision.HmacSha256("audit-key-1")
            : new(disposition);

        object first = Protect(decision, new byte[] { 0x01, 0x02 });
        object second = Protect(decision, new byte[] { 0x01, 0x03 });

        Assert.NotEqual(first, second);
        Assert.NotEqual(Protect(decision, "System.Byte[]"), first);
    }

    [Fact]
    public void TryProtect_TruncateDisposition_ByteArray_UsesHexRatherThanTypeName()
    {
        object protectedValue = Protect(
            new(ApplicationAuditValueDisposition.Truncate, MaximumLength: 64),
            new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        Assert.Equal("DEADBEEF", protectedValue);
    }

    [Fact]
    public void ToCanonicalString_Collections_ExpandElementsDistinctly()
    {
        Assert.Equal("[\"a\",\"b\"]", ApplicationAuditValueProtector.ToCanonicalString(new List<string> { "a", "b" }));
        Assert.NotEqual(
            ApplicationAuditValueProtector.ToCanonicalString(new List<string> { "a,b" }),
            ApplicationAuditValueProtector.ToCanonicalString(new List<string> { "a", "b" }));
    }

    [Fact]
    public void ToCanonicalString_FormattableValues_AreCultureInvariantAndPrecise()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("1.5", ApplicationAuditValueProtector.ToCanonicalString(1.5m));
            Assert.NotEqual(
                ApplicationAuditValueProtector.ToCanonicalString(new DateTime(2026, 1, 1, 0, 0, 0, 1, DateTimeKind.Utc)),
                ApplicationAuditValueProtector.ToCanonicalString(new DateTime(2026, 1, 1, 0, 0, 0, 2, DateTimeKind.Utc)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static object Protect(ApplicationAuditValueDecision decision, object? value)
    {
        Assert.True(ApplicationAuditValueProtector.TryProtect(
            new FixedDecisionPolicy(decision),
            typeof(string),
            "Field",
            value,
            out object protectedValue));
        return protectedValue;
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
