using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ProjectTemplate.Web.ErrorHandling;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the request-classification branches of <c>ProblemDetailsRequestClassifier</c> and the development-pipeline
/// and missing-options branches of <c>ProblemDetailsExtensions</c>.
/// </summary>
public sealed class ProblemDetailsBranchCoverageTests
{
    [Fact]
    public void ShouldWriteProblemDetails_HeadRequest_ReturnsFalseEvenForApiPath()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = HttpMethods.Head;
        httpContext.Request.Path = "/api/resource";

        Assert.False(ProblemDetailsRequestClassifier.ShouldWriteProblemDetails(httpContext));
    }

    [Fact]
    public void ShouldWriteProblemDetails_XmlHttpRequest_ReturnsTrue()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/page";
        httpContext.Request.Headers[HeaderNames.XRequestedWith] = "xmlhttprequest";

        Assert.True(ProblemDetailsRequestClassifier.ShouldWriteProblemDetails(httpContext));
    }

    [Theory]
    [InlineData("fetch", null)]
    [InlineData(null, "text/html")]
    [InlineData(null, null)]
    public void ShouldWriteProblemDetails_BrowserRequestWithoutJsonSignal_ReturnsFalse(
        string? requestedWith,
        string? accept)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/page";

        if (requestedWith is not null)
        {
            httpContext.Request.Headers[HeaderNames.XRequestedWith] = requestedWith;
        }

        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        Assert.False(ProblemDetailsRequestClassifier.ShouldWriteProblemDetails(httpContext));
    }

    [Fact]
    public async Task UseProblemDetails_DevelopmentEnvironment_ReturnsSameApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        // The empty builder registers no server, and building a WebApplication resolves IServer.
        _ = builder.WebHost.UseKestrelCore();
        await using WebApplication app = builder.Build();

        WebApplication result = app.UseProblemDetails();

        Assert.Same(app, result);
        Assert.True(app.Environment.IsDevelopment());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CustomizeProblemDetails_RequestLoggingOptionsNotRegistered_UsesTraceIdentifierAsCorrelationId(
        string? correlationHeaderValue)
    {
        ServiceCollection services = new();
        _ = services.AddApplicationProblemDetails(new TestWebHostEnvironment());
        using ServiceProvider provider = services.BuildServiceProvider();
        using ServiceProvider requestServicesWithoutOptions = new ServiceCollection().BuildServiceProvider();
        ProblemDetailsOptions options = provider
            .GetRequiredService<IOptions<ProblemDetailsOptions>>()
            .Value;
        DefaultHttpContext httpContext = new()
        {
            RequestServices = requestServicesWithoutOptions,
            TraceIdentifier = "trace-without-options"
        };

        if (correlationHeaderValue is not null)
        {
            httpContext.Request.Headers["X-Correlation-ID"] = correlationHeaderValue;
        }

        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status404NotFound
        };

        options.CustomizeProblemDetails!(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });

        Assert.Equal("trace-without-options", problemDetails.Extensions["correlationId"]);
        Assert.Equal("trace-without-options", problemDetails.Extensions["requestId"]);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ProjectTemplate.Web.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = Environments.Production;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
