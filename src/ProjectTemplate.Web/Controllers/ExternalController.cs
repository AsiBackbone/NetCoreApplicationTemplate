using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectTemplate.Web.Authentication;

namespace ProjectTemplate.Web.Controllers;

/// <summary>
/// Provides controller actions for initiating external authentication challenges using configured authentication
/// schemes.
/// </summary>
/// <remarks>This controller is intended for use with external authentication providers such as OAuth or OpenID
/// Connect. It ensures that only local return URLs are accepted to mitigate open redirect vulnerabilities. The
/// controller should be used in scenarios where users need to authenticate via third-party identity
/// providers.</remarks>
/// <param name="schemeProvider">The authentication scheme provider used to retrieve available external authentication schemes. Cannot be null.</param>
public sealed class ExternalController(IAuthenticationSchemeProvider schemeProvider) : Controller
{
    private readonly IAuthenticationSchemeProvider _schemeProvider = schemeProvider;

    /// <summary>
    /// Initiates an external authentication challenge using the specified provider.
    /// </summary>
    /// <remarks>This method is typically used to start an OAuth or other external login flow. Only local
    /// return URLs are permitted to prevent open redirect vulnerabilities.</remarks>
    /// <param name="provider">The name of the external authentication provider to use. Cannot be null, empty, or whitespace.</param>
    /// <param name="returnUrl">The URL to redirect the user to after successful authentication. If null or empty, defaults to the application's
    /// root ('/'). Must be a local URL.</param>
    /// <returns>An IActionResult that initiates the external authentication challenge or returns a BadRequest result if the
    /// input is invalid.</returns>
    [HttpGet("/External/Challenge")]
    [AllowAnonymous]
    public async Task<IActionResult> Challenge(string provider, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        string safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;

        if (!Url.IsLocalUrl(safeReturnUrl))
        {
            return BadRequest();
        }

        // Only schemes offered on the login page may be challenged. This excludes the cookie session scheme, the
        // default authenticate, sign-in, and sign-out schemes, and any scheme registered without a display name.
        AuthenticationScheme? scheme = await ExternalAuthenticationProviderSchemes
            .FindSelectableSchemeAsync(_schemeProvider, provider);

        if (scheme is null)
        {
            return BadRequest();
        }

        AuthenticationProperties properties = new()
        {
            RedirectUri = safeReturnUrl
        };

        return Challenge(properties, scheme.Name);
    }
}
