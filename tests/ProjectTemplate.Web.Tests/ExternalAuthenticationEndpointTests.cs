using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides integration tests for the baseline external authentication endpoints.
/// </summary>
public sealed class ExternalAuthenticationEndpointTests
{
    private const string _testExternalScheme = "TestExternal";
    private const string _testExternalDisplayName = "Test External";

    /// <summary>
    /// Verifies that a registered external provider can be challenged with a local return URL.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ExternalChallenge_ValidProviderAndLocalReturnUrl_RedirectsToReturnUrl()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/External/Challenge?provider={_testExternalScheme}&returnUrl=%2Fafter-login",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/after-login", response.Headers.Location.OriginalString);
    }

    /// <summary>
    /// Verifies that an unknown provider scheme is rejected safely.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ExternalChallenge_UnknownProvider_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/External/Challenge?provider=UnknownProvider&returnUrl=%2F",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that an external return URL is rejected to avoid open redirect behavior.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ExternalChallenge_ExternalReturnUrl_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        string returnUrl = Uri.EscapeDataString("https://evil.example/callback");

        using HttpResponseMessage response = await client.GetAsync(
            $"/External/Challenge?provider={_testExternalScheme}&returnUrl={returnUrl}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the local cookie scheme is not treated as an external provider challenge target.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ExternalChallenge_CookieScheme_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/External/Challenge?provider={CookieAuthenticationDefaults.AuthenticationScheme}&returnUrl=%2F",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the baseline login page renders registered external providers.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AccountLogin_RendersRegisteredExternalProviders()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/Account/Login?returnUrl=%2Fprotected",
            TestContext.Current.CancellationToken);

        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(_testExternalDisplayName, content, StringComparison.Ordinal);
        Assert.Contains(_testExternalScheme, content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the default login page explains that cookie authentication alone does not provide a sign-in path.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AccountLogin_WithDefaultProviderConfiguration_ExplainsMissingSignInPath()
    {
        using ApplicationWebApplicationFactory factory = new(new Dictionary<string, string?>());
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);

        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No sign-in provider is configured.", content, StringComparison.Ordinal);
        Assert.Contains("Cookie authentication stores authenticated sessions", content, StringComparison.Ordinal);
        Assert.DoesNotContain("/External/Challenge", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the login page rejects external return URLs.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AccountLogin_ExternalReturnUrl_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        string returnUrl = Uri.EscapeDataString("https://evil.example/login");

        using HttpResponseMessage response = await client.GetAsync(
            $"/Account/Login?returnUrl={returnUrl}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the access denied endpoint returns a safe forbidden response.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AccountAccessDenied_ReturnsForbidden()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/Account/AccessDenied",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Verifies that logout accepts a valid anti-forgery token, clears the local authentication cookie,
    /// and redirects only to the supplied local return URL.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AccountLogout_PostWithValidAntiforgeryToken_ClearsCookieAndRedirects()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithTestExternalProvider();
        using HttpClient client = factory.CreateHttpsClient();

        ClaimsPrincipal principal = CreateTestPrincipal();

        AuthenticationCookieContext authenticationCookieContext =
            CreateAuthenticationCookie(factory.Services, principal);

        AntiforgeryTokenContext antiforgeryTokenContext =
            CreateAntiforgeryToken(factory.Services, principal);

        client.DefaultRequestHeaders.Add(
            HeaderNames.Cookie,
            $"{authenticationCookieContext.CookieHeader}; {antiforgeryTokenContext.CookieHeader}");

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            [antiforgeryTokenContext.FormFieldName] = antiforgeryTokenContext.RequestToken,
            ["returnUrl"] = "/after-logout"
        });

        using HttpResponseMessage response = await client.PostAsync(
            "/Account/Logout",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/after-logout", response.Headers.Location.OriginalString);

        Assert.True(response.Headers.TryGetValues(HeaderNames.SetCookie, out IEnumerable<string>? setCookieHeaders));

        Assert.Contains(
            setCookieHeaders,
            header => header.StartsWith(
                $"{authenticationCookieContext.CookieName}=;",
                StringComparison.Ordinal));
    }
    /// <summary>
    /// Creates a test application factory with a test-only external authentication provider.
    /// </summary>
    /// <returns>A configured <see cref="WebApplicationFactory{TEntryPoint}"/> instance.</returns>
    private static WebApplicationFactory<Program> CreateFactoryWithTestExternalProvider()
    {
        return new ApplicationWebApplicationFactory(new Dictionary<string, string?>())
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services
                        .AddAuthentication()
                        .AddScheme<AuthenticationSchemeOptions, TestExternalChallengeAuthenticationHandler>(
                            _testExternalScheme,
                            _testExternalDisplayName,
                            _ => { })));
    }

    /// <summary>
    /// Creates a test ClaimsPrincipal instance with a predefined identity for use in authentication scenarios.
    /// </summary>
    /// <remarks>This method is intended for use in unit tests or development environments where a mock
    /// authenticated user is required.</remarks>
    /// <returns>A ClaimsPrincipal representing a test user with a fixed name and identifier.</returns>
    private static ClaimsPrincipal CreateTestPrincipal()
    {
        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "Test User")
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates an authentication cookie context for the specified principal using the configured cookie authentication
    /// options.
    /// </summary>
    /// <remarks>The returned context includes the cookie name and a value suitable for setting as a cookie in
    /// an HTTP response. The method uses the default cookie authentication scheme and the current options
    /// configuration.</remarks>
    /// <param name="services">The service provider used to resolve authentication and options services. Must not be null.</param>
    /// <param name="principal">The claims principal representing the authenticated user. Must not be null.</param>
    /// <returns>An AuthenticationCookieContext containing the cookie name and the protected authentication ticket value.</returns>
    private static AuthenticationCookieContext CreateAuthenticationCookie(
        IServiceProvider services,
        ClaimsPrincipal principal)
    {
        IOptionsMonitor<CookieAuthenticationOptions> optionsMonitor =
            services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        CookieAuthenticationOptions options =
            optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);

        AuthenticationTicket ticket = new(
            principal,
            new AuthenticationProperties(),
            CookieAuthenticationDefaults.AuthenticationScheme);

        string protectedTicket = options.TicketDataFormat.Protect(ticket);
        string cookieName = options.Cookie.Name ?? (CookieAuthenticationDefaults.CookiePrefix + CookieAuthenticationDefaults.AuthenticationScheme);

        return new AuthenticationCookieContext(
            cookieName,
            $"{cookieName}={protectedTicket}");
    }

    /// <summary>
    /// Creates a new antiforgery token context for the specified user and service provider.
    /// </summary>
    /// <remarks>This method generates and stores antiforgery tokens using the provided services and user
    /// context. The returned context includes all values necessary to submit a valid antiforgery token with a
    /// request.</remarks>
    /// <param name="services">The service provider used to resolve antiforgery services and dependencies. Cannot be null.</param>
    /// <param name="principal">The user principal for whom the antiforgery token is generated. Cannot be null.</param>
    /// <returns>An AntiforgeryTokenContext containing the form field name, request token, and cookie header for the antiforgery
    /// token.</returns>
    private static AntiforgeryTokenContext CreateAntiforgeryToken(
        IServiceProvider services,
        ClaimsPrincipal principal)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
            User = principal
        };

        IAntiforgery antiforgery = services.GetRequiredService<IAntiforgery>();
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(httpContext);

        string cookieHeader = (httpContext.Response.Headers[HeaderNames.SetCookie]
            .FirstOrDefault(header => !string.IsNullOrWhiteSpace(header))
            ?? throw new InvalidOperationException("Anti-forgery token generation did not create a Set-Cookie header."))
            .Split(';', 2)[0];

        return new AntiforgeryTokenContext(
            tokens.FormFieldName,
            tokens.RequestToken ?? string.Empty,
            cookieHeader);
    }

    /// <summary>
    /// Represents the context information for an authentication cookie, including its name and header value.
    /// </summary>
    /// <param name="CookieName">The name of the authentication cookie. This value is used to identify the cookie in HTTP requests and responses.</param>
    /// <param name="CookieHeader">The full header value of the authentication cookie as it appears in HTTP communication.</param>
    private sealed record AuthenticationCookieContext(
        string CookieName,
        string CookieHeader);

    /// <summary>
    /// Represents the context information required for antiforgery token validation in an HTTP request.
    /// </summary>
    /// <param name="FormFieldName">The name of the form field that contains the antiforgery token value. Cannot be null.</param>
    /// <param name="RequestToken">The antiforgery token value submitted with the request. May be null if not present.</param>
    /// <param name="CookieHeader">The value of the antiforgery cookie header from the HTTP request. May be null if the cookie is not set.</param>
    private sealed record AntiforgeryTokenContext(
        string FormFieldName,
        string RequestToken,
        string CookieHeader);
}
