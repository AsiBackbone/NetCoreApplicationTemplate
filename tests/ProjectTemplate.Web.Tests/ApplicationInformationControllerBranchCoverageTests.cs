using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectTemplate.Web.Controllers.Api;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the constructor guard and requested-version fallback branches of <c>ApplicationInformationController</c>.
/// </summary>
public sealed class ApplicationInformationControllerBranchCoverageTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            new ApplicationInformationController(null!));

        Assert.Equal("apiVersioningOptions", exception.ParamName);
    }

    [Fact]
    public void Get_WithoutVersioningFeature_DefaultsToVersionOneWithoutDeprecationHeaders()
    {
        DefaultHttpContext httpContext = new();
        ApplicationInformationController controller = CreateController(httpContext);

        ApplicationInformationResponse response = GetResponse(controller.Get());

        Assert.Equal("1.0", response.ApiVersion);
        Assert.Empty(httpContext.Response.Headers);
    }

    [Fact]
    public void Get_VersioningFeatureWithoutRequestedVersion_DefaultsToVersionOne()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Features.Set<IApiVersioningFeature>(new ApiVersioningFeature(httpContext)
        {
            RawRequestedApiVersions = []
        });
        ApplicationInformationController controller = CreateController(httpContext);

        ApplicationInformationResponse response = GetResponse(controller.Get());

        Assert.Equal("1.0", response.ApiVersion);
        Assert.Empty(httpContext.Response.Headers);
    }

    [Fact]
    public void Get_PreviewVersionOtherThanDeprecatedVersion_DoesNotAdvertiseDeprecation()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Features.Set<IApiVersioningFeature>(new ApiVersioningFeature(httpContext)
        {
            RequestedApiVersion = new ApiVersion(0, 8)
        });
        ApplicationInformationController controller = CreateController(httpContext);

        ApplicationInformationResponse response = GetResponse(controller.Get());

        Assert.Equal("0.8", response.ApiVersion);
        Assert.Empty(httpContext.Response.Headers);
    }

    private static ApplicationInformationController CreateController(DefaultHttpContext httpContext)
    {
        return new ApplicationInformationController(
            Microsoft.Extensions.Options.Options.Create(new ApplicationApiVersioningOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ApplicationInformationResponse GetResponse(ActionResult<ApplicationInformationResponse> result)
    {
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ApplicationInformationResponse>(okResult.Value);
    }
}
