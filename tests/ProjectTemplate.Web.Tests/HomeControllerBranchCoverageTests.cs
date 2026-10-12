using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectTemplate.Web.Controllers;
using ProjectTemplate.Web.Models;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the status-code resolution branches of <c>HomeController.Error</c> that the error-handling pipeline does not
/// reach on its own: a missing route status code and status codes outside the error-page range.
/// </summary>
public sealed class HomeControllerBranchCoverageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(302)]
    [InlineData(700)]
    public void Error_ExceptionWithMissingOrOutOfRangeRouteStatus_UsesInternalServerError(int? routeStatusCode)
    {
        DefaultHttpContext httpContext = new() { TraceIdentifier = "home-error-exception" };
        httpContext.Features.Set<IExceptionHandlerPathFeature>(new TestExceptionHandlerPathFeature
        {
            Path = "/failing-path",
            Error = new InvalidOperationException("Expected test failure.")
        });
        HomeController controller = CreateController(httpContext);

        ErrorViewModel model = GetModel(controller.Error(routeStatusCode));

        Assert.Equal(StatusCodes.Status500InternalServerError, model.StatusCode);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("home-error-exception", model.RequestId);
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status200OK, StatusCodes.Status500InternalServerError)]
    public void Error_StatusCodeReExecution_PrefersPipelineStatusWithinErrorRange(
        int originalStatusCode,
        int expectedStatusCode)
    {
        DefaultHttpContext httpContext = new() { TraceIdentifier = "home-error-status" };
        httpContext.Features.Set<IStatusCodeReExecuteFeature>(new TestStatusCodeReExecuteFeature
        {
            OriginalPath = "/missing-page",
            OriginalStatusCode = originalStatusCode
        });
        HomeController controller = CreateController(httpContext);

        ErrorViewModel model = GetModel(controller.Error(StatusCodes.Status418ImATeapot));

        Assert.Equal(expectedStatusCode, model.StatusCode);
        Assert.Equal(expectedStatusCode, httpContext.Response.StatusCode);
    }

    private static HomeController CreateController(DefaultHttpContext httpContext)
    {
        return new HomeController(NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ErrorViewModel GetModel(IActionResult result)
    {
        ViewResult view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<ErrorViewModel>(view.Model);
    }

    private sealed class TestExceptionHandlerPathFeature : IExceptionHandlerPathFeature
    {
        public Exception Error { get; set; } = new InvalidOperationException();

        public string Path { get; set; } = string.Empty;

        public Endpoint? Endpoint { get; set; }

        public RouteValueDictionary? RouteValues { get; set; }
    }

    private sealed class TestStatusCodeReExecuteFeature : IStatusCodeReExecuteFeature
    {
        public string OriginalPathBase { get; set; } = string.Empty;

        public string OriginalPath { get; set; } = string.Empty;

        public string? OriginalQueryString { get; set; }

        public int OriginalStatusCode { get; set; }
    }
}
