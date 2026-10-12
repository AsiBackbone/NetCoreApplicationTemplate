using ProjectTemplate.Infrastructure.Data.Auditing;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the required-value guard and optional-value normalization of <c>ApplicationAuditContext</c>.
/// </summary>
public sealed class ApplicationAuditContextBranchCoverageTests
{
    [Theory]
    [InlineData("", "Human", "Display Name", "actorId")]
    [InlineData("actor-1", " ", "Display Name", "actorType")]
    [InlineData("actor-1", "Human", "\t", "actorDisplayName")]
    public void Constructor_BlankRequiredValue_ThrowsArgumentException(
        string actorId,
        string actorType,
        string actorDisplayName,
        string expectedParameterName)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new ApplicationAuditContext(actorId, actorType, actorDisplayName));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void Constructor_PaddedValues_TrimsRequiredValuesAndDropsBlankOptionalValues()
    {
        ApplicationAuditContext context = new(
            " actor-1 ",
            " Human ",
            " Display Name ",
            correlationId: " ",
            traceId: " trace-1 ");

        Assert.Equal("actor-1", context.ActorId);
        Assert.Equal("Human", context.ActorType);
        Assert.Equal("Display Name", context.ActorDisplayName);
        Assert.Null(context.CorrelationId);
        Assert.Equal("trace-1", context.TraceId);
    }
}
