using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Infrastructure.Data.Entities;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Pins the privacy-affecting defaults a generated application starts with.
/// </summary>
/// <remarks>
/// These defaults decide what personal data a generated application writes without its consumer having chosen to.
/// Audit rows and request logs are both durable and typically retained longer than the data they describe, so a
/// permissive default here is difficult to undo after the fact.
/// </remarks>
public sealed class AuditAndLoggingPrivacyDefaultsTests
{
    /// <summary>
    /// The contact fields on <see cref="ExternalLoginAccount" /> that the default policy masks.
    /// </summary>
    public static TheoryData<string> MaskedExternalLoginAccountProperties =>
    [
        nameof(ExternalLoginAccount.Email),
        nameof(ExternalLoginAccount.NormalizedEmail),
        nameof(ExternalLoginAccount.DisplayName)
    ];

    /// <summary>
    /// Verifies that the default policy masks the contact fields carried by an external login account.
    /// </summary>
    /// <param name="propertyName">The property expected to be masked.</param>
    [Theory]
    [MemberData(nameof(MaskedExternalLoginAccountProperties))]
    public void DefaultPolicyMasksExternalLoginAccountContactFields(string propertyName)
    {
        var policy = new DefaultApplicationAuditValuePolicy();

        ApplicationAuditValueDecision decision = policy.Evaluate(
            typeof(ExternalLoginAccount),
            propertyName,
            "person@example.com");

        Assert.Equal(ApplicationAuditValueDisposition.Mask, decision.Disposition);
    }

    /// <summary>
    /// Verifies that the default policy still includes non-personal properties, so audit records stay useful.
    /// </summary>
    /// <param name="propertyName">The property expected to be included unchanged.</param>
    [Theory]
    [InlineData(nameof(ExternalLoginAccount.ProviderName))]
    [InlineData(nameof(ExternalLoginAccount.NormalizedProviderName))]
    [InlineData(nameof(ExternalLoginAccount.LocalUserId))]
    public void DefaultPolicyIncludesNonPersonalExternalLoginAccountProperties(string propertyName)
    {
        var policy = new DefaultApplicationAuditValuePolicy();

        ApplicationAuditValueDecision decision = policy.Evaluate(
            typeof(ExternalLoginAccount),
            propertyName,
            "value");

        Assert.Equal(ApplicationAuditValueDisposition.Include, decision.Disposition);
    }

    /// <summary>
    /// Verifies that the masking is scoped to the entity it was written for rather than applied by property name.
    /// </summary>
    [Fact]
    public void DefaultPolicyDoesNotMaskSimilarlyNamedPropertiesOnOtherEntities()
    {
        var policy = new DefaultApplicationAuditValuePolicy();

        ApplicationAuditValueDecision decision = policy.Evaluate(
            typeof(UnrelatedEntity),
            nameof(UnrelatedEntity.Email),
            "person@example.com");

        Assert.Equal(ApplicationAuditValueDisposition.Include, decision.Disposition);
    }

    /// <summary>
    /// Verifies that request logging does not record user names or client IP addresses unless enabled.
    /// </summary>
    [Fact]
    public void RequestLoggingDoesNotRecordPersonalFieldsByDefault()
    {
        var options = new ApplicationRequestLoggingOptions();

        Assert.False(options.IncludeUserName);
        Assert.False(options.IncludeRemoteIpAddress);
        Assert.False(options.IncludeQueryString);
    }

    private sealed class UnrelatedEntity
    {
        public string? Email { get; set; }
    }
}
