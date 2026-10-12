using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the null and padded-value branches of the data access option helpers <c>AuditStorageModes</c> and
/// <c>DataAccessOptions</c>.
/// </summary>
public sealed class DataAccessOptionValuesBranchCoverageTests
{
    [Fact]
    public void AuditStorageModes_NullStorageMode_NormalizesToEmptyAndIsNotSupported()
    {
        Assert.Empty(AuditStorageModes.Normalize(null));
        Assert.False(AuditStorageModes.IsSupported(null));
        Assert.False(AuditStorageModes.IsLocal(null));
    }

    [Theory]
    [InlineData(" local ", true, true)]
    [InlineData("OUTBOX", true, false)]
    [InlineData(" ExternalSink ", true, false)]
    [InlineData("Remote", false, false)]
    public void AuditStorageModes_PaddedOrCasedStorageMode_IsClassifiedAfterNormalization(
        string storageMode,
        bool expectedSupported,
        bool expectedLocal)
    {
        Assert.Equal(expectedSupported, AuditStorageModes.IsSupported(storageMode));
        Assert.Equal(expectedLocal, AuditStorageModes.IsLocal(storageMode));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(" ", false)]
    [InlineData(" none ", true)]
    [InlineData("DISABLED", true)]
    [InlineData("Sqlite", false)]
    public void DataAccessOptions_IsDisabledProvider_NormalizesBeforeComparing(
        string? provider,
        bool expectedDisabled)
    {
        Assert.Equal(expectedDisabled, DataAccessOptions.IsDisabledProvider(provider));
    }
}
