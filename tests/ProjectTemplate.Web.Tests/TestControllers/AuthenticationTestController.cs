using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectTemplate.Web.Authentication.Extensions;

namespace ProjectTemplate.Web.Tests.TestControllers;

/// <summary>
/// Provides test endpoints for verifying application authentication and authorization behavior.
/// </summary>
[ApiController]
[Route("test/authentication")]
public sealed class AuthenticationTestController : ControllerBase
{
    /// <summary>
    /// Returns an anonymous response that does not require authentication.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing an anonymous result.</returns>
    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult Anonymous()
    {
        return Ok(new { result = "anonymous" });
    }

    /// <summary>
    /// Establishes a test-only cookie-authenticated principal through the application's configured cookie scheme.
    /// </summary>
    /// <param name="userName">The test user name to persist in the authentication cookie.</param>
    /// <returns>A task that represents the asynchronous sign-in operation.</returns>
    [HttpPost("cookie-sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> CookieSignIn(string userName = "cookie-test-user")
    {
        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, userName),
            new Claim(ClaimTypes.Name, userName)
        ];

        ClaimsIdentity identity = new(claims, "Cookies");
        ClaimsPrincipal principal = new(identity);

        await HttpContext.SignInAsync(
            "Cookies",
            principal);

        return Ok(new { result = "signed-in" });
    }

    /// <summary>
    /// Returns an unannotated response governed by the fallback authorization policy.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing a fallback-policy result.</returns>
    [HttpGet("fallback")]
    public IActionResult Fallback()
    {
        return Ok(new { result = "fallback" });
    }

    /// <summary>
    /// Returns a protected response that requires an authenticated user.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing the protected result and observed identity.</returns>
    [HttpGet("protected")]
    [Authorize(Policy = ApplicationAuthorizationPolicyNames.AuthenticatedUser)]
    public IActionResult Protected()
    {
        return Ok(new
        {
            result = "protected",
            userName = User.Identity?.Name,
            subject = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });
    }

    /// <summary>
    /// Returns a test response for users who satisfy the administrator role authorization policy.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing an administrator authorization test result.</returns>
    [HttpGet("admin")]
    [Authorize(Policy = ApplicationAuthorizationPolicyNames.AdministratorRole)]
    public IActionResult Admin()
    {
        return Ok(new { result = "admin" });
    }

    /// <summary>
    /// Returns a test response for users who satisfy the manage application permission authorization policy.
    /// </summary>
    /// <returns>An <see cref="IActionResult"/> containing a manage application permission authorization test result.</returns>
    [HttpGet("manage")]
    [Authorize(Policy = ApplicationAuthorizationPolicyNames.ManageApplicationPermission)]
    public IActionResult Manage()
    {
        return Ok(new { result = "manage" });
    }
}
