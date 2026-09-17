namespace ProjectTemplate.Web.Authentication.Providers.OpenIdConnect;

/// <summary>
/// Represents OpenID Connect authentication provider configuration.
/// </summary>
public sealed class OpenIdConnectAuthenticationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether OpenID Connect authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the OpenID Connect authentication scheme.
    /// </summary>
    public string Scheme { get; set; } = "OpenIdConnect";

    /// <summary>
    /// Gets or sets the display name shown for the OpenID Connect provider.
    /// </summary>
    public string DisplayName { get; set; } = "OpenID Connect";

    /// <summary>
    /// Gets or sets the OpenID Connect authority URL.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenID Connect client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenID Connect client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the callback path used by the OpenID Connect handler.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Gets or sets the OpenID Connect response type.
    /// </summary>
    public string ResponseType { get; set; } = "code";

    /// <summary>
    /// Gets or sets a value indicating whether provider tokens are stored in the authentication cookie after sign-in.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>. Saved tokens travel with the authentication cookie on every request, which
    /// grows the cookie and keeps access, identity, and refresh tokens on the client. Enable this only when the
    /// application calls downstream APIs with the provider's tokens and has a documented protection and lifetime
    /// strategy; prefer server-side token storage for anything longer-lived.
    /// </remarks>
    public bool SaveTokens { get; set; }

    /// <summary>
    /// Gets or sets the requested OpenID Connect scopes.
    /// </summary>
    public string[] Scopes { get; set; } =
    [
        "openid",
        "profile",
        "email"
    ];
}
