using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Options;

namespace ProjectTemplate.Web.Controllers.Api;

/// <summary>
/// Provides sample versioned API endpoints for the application.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("0.9", Deprecated = true)]
[Route("api/v{version:apiVersion}/application-information")]
[Route("api/application-information")]
public sealed class ApplicationInformationController(
    IOptions<ApplicationApiVersioningOptions> apiVersioningOptions) : ControllerBase
{
    private const string _deprecationHeaderName = "Deprecation";
    private const string _sunsetHeaderName = "Sunset";

    private readonly ApplicationApiVersioningOptions _apiVersioningOptions =
        apiVersioningOptions?.Value ?? throw new ArgumentNullException(nameof(apiVersioningOptions));

    /// <summary>
    /// Returns application API information for the requested API version.
    /// </summary>
    /// <returns>Application API version information.</returns>
    [HttpGet]
    public ActionResult<ApplicationInformationResponse> Get()
    {
        ApiVersion requestedVersion = HttpContext.Features
            .Get<IApiVersioningFeature>()
            ?.RequestedApiVersion ?? new ApiVersion(1, 0);

        if (requestedVersion.MajorVersion == 0 && requestedVersion.MinorVersion == 9)
        {
            AppendDeprecationHeaders();
        }

        return Ok(new ApplicationInformationResponse(
            ApplicationName: "ProjectTemplate.Web",
            ApiVersion: FormatApiVersion(requestedVersion),
            Message: "API versioning foundation active."));
    }

    private static string FormatApiVersion(ApiVersion version)
    {
        int minorVersion = version.MinorVersion ?? 0;

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{version.MajorVersion}.{minorVersion}");
    }

    private void AppendDeprecationHeaders()
    {
        Response.Headers[_deprecationHeaderName] = "true";
        Response.Headers.Link = "</api/v1/application-information>; rel=\"successor-version\"";

        // Sunset is advertised only when the application has configured a real removal date. A template cannot
        // supply one: a shipped date is arbitrary until it passes, and misleading afterwards.
        if (_apiVersioningOptions.DeprecatedVersionSunset is DateTimeOffset sunsetDate)
        {
            Response.Headers[_sunsetHeaderName] =
                sunsetDate.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>
/// Represents sample application API information.
/// </summary>
/// <param name="ApplicationName">The application name.</param>
/// <param name="ApiVersion">The resolved API version.</param>
/// <param name="Message">A short status message.</param>
public sealed record ApplicationInformationResponse(
    string ApplicationName,
    string ApiVersion,
    string Message);
