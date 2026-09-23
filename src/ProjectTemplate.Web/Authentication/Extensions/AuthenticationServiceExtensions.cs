using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Authentication.Claims;
using ProjectTemplate.Web.Authentication.Options;
using ProjectTemplate.Web.Authentication.Providers.GitHub;
using ProjectTemplate.Web.Authentication.Providers.Google;
using ProjectTemplate.Web.Authentication.Providers.Microsoft;
using ProjectTemplate.Web.Authentication.Providers.OpenIdConnect;
using ProjectTemplate.Web.Authentication.Providers.Saml2;

namespace ProjectTemplate.Web.Authentication.Extensions;

/// <summary>
/// Provides extension methods for registering and applying application authentication services.
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// The authentication cookie name used when the cookie is always sent with the <c>Secure</c> attribute.
    /// </summary>
    public const string HostPrefixedAuthenticationCookieName = "__Host-ProjectTemplate.Web.Authentication";

    /// <summary>
    /// The authentication cookie name used when the Development-only insecure HTTP override is active.
    /// </summary>
    public const string AuthenticationCookieName = ".ProjectTemplate.Web.Authentication";

    /// <summary>
    /// Adds application authentication services based on configuration using the secure cookie policy without an
    /// environment-specific plain HTTP override.
    /// </summary>
    /// <param name="services">The service collection to add authentication services to.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return AddApplicationAuthentication(services, configuration, environment: null);
    }

    /// <summary>
    /// Adds application authentication services based on configuration and the current hosting environment.
    /// </summary>
    /// <param name="services">The service collection to add authentication services to.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <param name="environment">The current hosting environment.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddTransient<IClaimsTransformation, ApplicationClaimsTransformation>();
        services.AddSingleton<IValidateOptions<ApplicationAuthenticationOptions>>(
            new ApplicationAuthenticationOptionsValidator(environment));

        services
            .AddOptions<ApplicationAuthenticationOptions>()
            .Bind(configuration.GetSection(ApplicationAuthenticationOptions.SectionName))
            .ValidateOnStart();

        AuthenticationBuilder authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        });
        services
            .AddOptions<AuthenticationOptions>()
            .Configure<IOptions<ApplicationAuthenticationOptions>>((options, applicationAuthenticationOptionsAccessor) =>
            {
                ApplicationAuthenticationOptions applicationAuthenticationOptions =
                    applicationAuthenticationOptionsAccessor.Value;

                if (!applicationAuthenticationOptions.Enabled)
                {
                    return;
                }

                options.DefaultScheme = applicationAuthenticationOptions.DefaultScheme;
                options.DefaultAuthenticateScheme = applicationAuthenticationOptions.DefaultScheme;
                options.DefaultChallengeScheme = applicationAuthenticationOptions.DefaultChallengeScheme;
                options.DefaultSignInScheme = applicationAuthenticationOptions.DefaultSignInScheme;
            });

        authenticationBuilder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

        services
            .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<ApplicationAuthenticationOptions>>((options, applicationAuthenticationOptionsAccessor) =>
            {
                ApplicationAuthenticationOptions applicationAuthenticationOptions =
                    applicationAuthenticationOptionsAccessor.Value;

                options.LoginPath = applicationAuthenticationOptions.Cookie.LoginPath;
                options.LogoutPath = applicationAuthenticationOptions.Cookie.LogoutPath;
                options.AccessDeniedPath = applicationAuthenticationOptions.Cookie.AccessDeniedPath;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(applicationAuthenticationOptions.Cookie.ExpireMinutes);
                options.SlidingExpiration = applicationAuthenticationOptions.Cookie.SlidingExpiration;

                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = applicationAuthenticationOptions.Cookie.AllowInsecureHttp &&
                    environment?.IsDevelopment() == true
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;

                // Browsers accept a __Host- cookie only when it is Secure, has Path=/, and has no Domain, which binds
                // the session cookie to this exact host. The prefix is used only when the cookie is always Secure, so
                // the Development-only insecure HTTP override keeps working with an unprefixed name.
                bool useHostPrefix = options.Cookie.SecurePolicy == CookieSecurePolicy.Always;
                options.Cookie.Name = useHostPrefix
                    ? HostPrefixedAuthenticationCookieName
                    : AuthenticationCookieName;
                if (useHostPrefix)
                {
                    options.Cookie.Path = "/";
                }
            });

        ApplicationAuthenticationOptions applicationAuthenticationOptions = configuration
            .GetSection(ApplicationAuthenticationOptions.SectionName)
            .Get<ApplicationAuthenticationOptions>() ?? new ApplicationAuthenticationOptions();

        authenticationBuilder
            .AddOpenIdConnectAuthentication(applicationAuthenticationOptions.Providers.OpenIdConnect)
            .AddSaml2Authentication(applicationAuthenticationOptions.Providers.Saml2)
            .AddMicrosoftAuthentication(applicationAuthenticationOptions.Providers.Microsoft)
            .AddGoogleAuthentication(applicationAuthenticationOptions.Providers.Google)
            .AddGitHubAuthentication(applicationAuthenticationOptions.Providers.GitHub);

        return services;
    }

    /// <summary>
    /// Applies application authentication middleware when authentication is enabled.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance for chaining.</returns>
    public static IApplicationBuilder UseApplicationAuthentication(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ApplicationAuthenticationOptions options = app.ApplicationServices
            .GetRequiredService<IOptions<ApplicationAuthenticationOptions>>()
            .Value;

        return !options.Enabled ? app : app.UseAuthentication();
    }
}
