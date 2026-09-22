using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
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
    /// Issues a test-only antiforgery request token and stores the paired antiforgery cookie.
    /// </summary>
    /// <param name="antiforgery">The ASP.NET Core antiforgery service.</param>
    /// <returns>The request token required by the cookie sign-in POST.</returns>
    [HttpGet("antiforgery-token")]
    [AllowAnonymous]
    public IActionResult AntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Content(tokens.RequestToken ?? string.Empty);
    }

    /// <summary>
    /// Establishes a test-only cookie-authenticated principal through the application's configured cookie scheme.
    /// </summary>
    /// <param name="userName">The test user name to persist in the authentication cookie.</param>
    /// <returns>A task that represents the asynchronous sign-in operation.</returns>
    [HttpPost("cookie-sign-in")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CookieSignIn([FromForm] string userName = "cookie-test-user")
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
    /// Accepts an unsafe (PUT) request that carries no antiforgery attribute, so only the global
    /// AutoValidateAntiforgeryTokenAttribute filter protects it.
    /// </summary>
    /// <remarks>
    /// PUT rather than POST: static analysis (CodeQL cs/web/missing-token-validation) cannot see globally
    /// registered MVC filters and would flag an unannotated POST, while the global filter covers every unsafe verb.
    /// </remarks>
    /// <returns>An OK response when the request passes antiforgery validation.</returns>
    [HttpPut("unannotated-unsafe")]
    [AllowAnonymous]
    public IActionResult UnannotatedUnsafe()
    {
        return Ok(new { result = "accepted" });
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
