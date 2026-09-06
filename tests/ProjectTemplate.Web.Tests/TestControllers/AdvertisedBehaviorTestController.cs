using Microsoft.AspNetCore.Mvc;

namespace ProjectTemplate.Web.Tests.TestControllers;

/// <summary>
/// Provides test endpoints used to verify v1.0 advertised runtime behavior.
/// </summary>
[ApiController]
[Route("test/advertised-behavior")]
public sealed class AdvertisedBehaviorTestController : ControllerBase
{
    /// <summary>
    /// Returns request information after middleware has processed forwarded headers.
    /// </summary>
    /// <returns>Request scheme, host, and remote IP information.</returns>
    [HttpGet("request-info")]
    public IActionResult RequestInfo()
    {
        return Ok(new
        {
            scheme = Request.Scheme,
            host = Request.Host.Value,
            remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
    }

    /// <summary>
    /// Throws an argument exception to verify API-style Problem Details handling.
    /// </summary>
    /// <returns>Never returns; the action always throws.</returns>
    /// <exception cref="ArgumentException">Always thrown for test coverage.</exception>
    /// <remarks>
    /// This must stay an instance method. MVC does not discover static methods as actions, so declaring it static
    /// made the route unreachable and every request to it returned 404 instead of exercising the error pipeline.
    /// </remarks>
#pragma warning disable CA1822 // Mark members as static
    [HttpGet("problem-details")]
    public IActionResult ProblemDetailsException()
#pragma warning restore CA1822
    {
        throw new ArgumentException("Invalid advertised behavior test request.");
    }
}
