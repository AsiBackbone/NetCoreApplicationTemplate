using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProjectTemplate.Web.Diagnostics;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies that diagnostic log entries written outside the request-logging middleware honor the request-logging
/// remote IP address privacy option.
/// </summary>
public sealed class RequestLoggingPrivacyTests
{
    private static readonly IPAddress _remoteIpAddress = IPAddress.Parse("203.0.113.10");

    /// <summary>
    /// Verifies that the remote IP address is withheld when request logging uses its default options.
    /// </summary>
    [Fact]
    public void GetLoggableRemoteIpAddress_ReturnsNull_ByDefault()
    {
        using ServiceProvider services = CreateServices(new ApplicationRequestLoggingOptions());

        string? remoteIpAddress = RequestLoggingPrivacy.GetLoggableRemoteIpAddress(CreateHttpContext(services));

        Assert.Null(remoteIpAddress);
    }

    /// <summary>
    /// Verifies that the remote IP address is returned when request logging explicitly opts in.
    /// </summary>
    [Fact]
    public void GetLoggableRemoteIpAddress_ReturnsAddress_WhenIncludeRemoteIpAddressIsEnabled()
    {
        using ServiceProvider services = CreateServices(
            new ApplicationRequestLoggingOptions { IncludeRemoteIpAddress = true });

        string? remoteIpAddress = RequestLoggingPrivacy.GetLoggableRemoteIpAddress(CreateHttpContext(services));

        Assert.Equal("203.0.113.10", remoteIpAddress);
    }

    /// <summary>
    /// Verifies that the privacy-preserving default applies when request logging options are not registered.
    /// </summary>
    [Fact]
    public void GetLoggableRemoteIpAddress_ReturnsNull_WhenOptionsAreNotRegistered()
    {
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        string? remoteIpAddress = RequestLoggingPrivacy.GetLoggableRemoteIpAddress(CreateHttpContext(services));

        Assert.Null(remoteIpAddress);
    }

    /// <summary>
    /// Verifies that the privacy-preserving default applies when the request has no service provider.
    /// </summary>
    [Fact]
    public void GetLoggableRemoteIpAddress_ReturnsNull_WhenRequestServicesAreUnavailable()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Connection.RemoteIpAddress = _remoteIpAddress;

        string? remoteIpAddress = RequestLoggingPrivacy.GetLoggableRemoteIpAddress(httpContext);

        Assert.Null(remoteIpAddress);
    }

    private static ServiceProvider CreateServices(ApplicationRequestLoggingOptions options)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services
        };
        httpContext.Connection.RemoteIpAddress = _remoteIpAddress;

        return httpContext;
    }
}
