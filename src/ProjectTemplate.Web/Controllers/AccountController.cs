using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectTemplate.Web.Authentication;
using ProjectTemplate.Web.Models;

namespace ProjectTemplate.Web.Controllers;

/// <summary>
/// Provides actions for user authentication, including login, logout, and access denied handling.
/// </summary>
/// <remarks>This controller exposes endpoints for user authentication workflows. It supports external
/// authentication providers and enforces security best practices such as requiring local return URLs to prevent open
/// redirect vulnerabilities. Actions are decorated with appropriate authorization and anti-forgery attributes as
/// needed.</remarks>
/// <param name="schemeProvider">The authentication scheme provider used to retrieve available external authentication schemes for login operations.
/// Cannot be null.</param>
public class AccountController(IAuthenticationSchemeProvider schemeProvider) : Controller
{
    private readonly IAuthenticationSchemeProvider _schemeProvider = schemeProvider;

    /// <summary>
    /// Displays the login page and provides available external authentication providers.
    /// </summary>
    /// <remarks>This action is accessible without authentication. Only local return URLs are permitted to
    /// prevent open redirect vulnerabilities.</remarks>
    /// <param name="returnUrl">The URL to redirect to after a successful login. If null or empty, the user is redirected to the application's
    /// root. Must be a local URL.</param>
    /// <returns>A view result that renders the login page with available external authentication providers, or a bad request
    /// result if the return URL is not local.</returns>
    [HttpGet("/Account/Login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        string safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;

        if (!Url.IsLocalUrl(safeReturnUrl))
        {
            return BadRequest();
        }

        IReadOnlyList<AuthenticationScheme> schemes = await ExternalAuthenticationProviderSchemes
            .GetSelectableSchemesAsync(_schemeProvider);

        AccountLoginViewModel model = new()
        {
            ReturnUrl = safeReturnUrl,
            ExternalProviders = schemes
                .Select(scheme => new ExternalAuthenticationProviderViewModel
                {
                    Scheme = scheme.Name,
                    DisplayName = scheme.DisplayName ?? scheme.Name
                })
                .OrderBy(provider => provider.DisplayName)
                .ToList()
        };

        return View(model);
    }

    /// <summary>
    /// Signs out the current user and redirects to the specified return URL.
    /// </summary>
    /// <remarks>This action requires an authenticated user, a valid anti-forgery token, and only accepts local URLs for redirection
    /// to help prevent cross-site request forgery and open redirect vulnerabilities.</remarks>
    /// <param name="returnUrl">The URL to redirect to after sign-out. If null or empty, defaults to the application's root ('/'). Must be a
    /// local URL.</param>
    /// <returns>A redirect result to the specified local return URL if sign-out is successful; otherwise, a bad request result
    /// if the return URL is not local.</returns>
    [Authorize]
    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        string safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;

        if (!Url.IsLocalUrl(safeReturnUrl))
        {
            return BadRequest();
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return LocalRedirect(safeReturnUrl);
    }

    /// <summary>
    /// Handles requests to the access denied page and returns a view indicating that the user does not have permission
    /// to access the requested resource.
    /// </summary>
    /// <remarks>This action is accessible to all users, including unauthenticated users. The response status
    /// code is set to 403 to indicate forbidden access.</remarks>
    /// <returns>A view result that displays the access denied page with a 403 Forbidden status code.</returns>
    [HttpGet("/Account/AccessDenied")]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
