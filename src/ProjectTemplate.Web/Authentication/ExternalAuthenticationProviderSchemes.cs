using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ProjectTemplate.Web.Authentication;

/// <summary>
/// Determines which registered authentication schemes a user may choose as an external sign-in provider.
/// </summary>
/// <remarks>
/// The login page and the external challenge endpoint use the same rule, so a scheme can be challenged through
/// <c>/External/Challenge</c> only when it is also offered on the login page. A scheme is selectable when it has a
/// non-empty display name and is not a reserved local scheme: the cookie session scheme or the default authenticate,
/// sign-in, or sign-out scheme. Register a display name only for schemes users are meant to choose; schemes registered
/// without one, such as bearer-token or test schemes, cannot be challenged through the external endpoint.
/// </remarks>
internal static class ExternalAuthenticationProviderSchemes
{
    /// <summary>
    /// Gets the registered schemes that users may choose as external sign-in providers.
    /// </summary>
    /// <param name="schemeProvider">The authentication scheme provider.</param>
    /// <returns>The selectable external provider schemes.</returns>
    internal static async Task<IReadOnlyList<AuthenticationScheme>> GetSelectableSchemesAsync(
        IAuthenticationSchemeProvider schemeProvider)
    {
        ArgumentNullException.ThrowIfNull(schemeProvider);

        HashSet<string> reservedSchemeNames = await GetReservedSchemeNamesAsync(schemeProvider).ConfigureAwait(false);
        IEnumerable<AuthenticationScheme> schemes = await schemeProvider.GetAllSchemesAsync().ConfigureAwait(false);

        return [.. schemes.Where(scheme => IsSelectable(scheme, reservedSchemeNames))];
    }

    /// <summary>
    /// Finds a selectable external provider scheme by name.
    /// </summary>
    /// <param name="schemeProvider">The authentication scheme provider.</param>
    /// <param name="schemeName">The scheme name supplied by the request.</param>
    /// <returns>The scheme when it exists and is selectable; otherwise <see langword="null"/>.</returns>
    internal static async Task<AuthenticationScheme?> FindSelectableSchemeAsync(
        IAuthenticationSchemeProvider schemeProvider,
        string? schemeName)
    {
        ArgumentNullException.ThrowIfNull(schemeProvider);

        if (string.IsNullOrWhiteSpace(schemeName))
        {
            return null;
        }

        AuthenticationScheme? scheme = await schemeProvider.GetSchemeAsync(schemeName).ConfigureAwait(false);

        if (scheme is null)
        {
            return null;
        }

        HashSet<string> reservedSchemeNames = await GetReservedSchemeNamesAsync(schemeProvider).ConfigureAwait(false);

        return IsSelectable(scheme, reservedSchemeNames) ? scheme : null;
    }

    private static bool IsSelectable(AuthenticationScheme scheme, HashSet<string> reservedSchemeNames)
    {
        return !string.IsNullOrWhiteSpace(scheme.DisplayName) &&
            !reservedSchemeNames.Contains(scheme.Name);
    }

    private static async Task<HashSet<string>> GetReservedSchemeNamesAsync(IAuthenticationSchemeProvider schemeProvider)
    {
        HashSet<string> reservedSchemeNames = new(StringComparer.Ordinal)
        {
            CookieAuthenticationDefaults.AuthenticationScheme
        };

        AddSchemeName(reservedSchemeNames, await schemeProvider.GetDefaultAuthenticateSchemeAsync().ConfigureAwait(false));
        AddSchemeName(reservedSchemeNames, await schemeProvider.GetDefaultSignInSchemeAsync().ConfigureAwait(false));
        AddSchemeName(reservedSchemeNames, await schemeProvider.GetDefaultSignOutSchemeAsync().ConfigureAwait(false));

        return reservedSchemeNames;
    }

    private static void AddSchemeName(HashSet<string> schemeNames, AuthenticationScheme? scheme)
    {
        if (scheme is not null)
        {
            _ = schemeNames.Add(scheme.Name);
        }
    }
}
