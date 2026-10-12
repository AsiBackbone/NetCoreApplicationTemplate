using ProjectTemplate.Web.Middleware;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the constructor guards of <c>SecurityHeadersMiddleware</c>.
/// </summary>
public sealed class SecurityHeadersMiddlewareBranchCoverageTests
{
    [Fact]
    public void Constructor_NullNext_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new SecurityHeadersMiddleware(
            null!,
            Microsoft.Extensions.Options.Options.Create(new ApplicationSecurityHeadersOptions())));

        Assert.Equal("next", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullOptionsValue_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create<ApplicationSecurityHeadersOptions>(null!)));

        Assert.Equal("options", exception.ParamName);
    }
}
