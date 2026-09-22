using System.Net;
using ProjectTemplate.Web.Tests.Extensions;
using ProjectTemplate.Web.Tests.Infrastructure;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Verifies that antiforgery validation is applied globally to unsafe MVC requests.
/// </summary>
public sealed class AntiforgeryTests
{
    private const string _unannotatedPostPath = "/test/authentication/unannotated-post";

    /// <summary>
    /// Verifies that a POST to an action without an antiforgery attribute is rejected when the token is missing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnannotatedPost_WithoutAntiforgeryToken_IsRejected()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();
        using FormUrlEncodedContent content = new(new Dictionary<string, string>());

        using HttpResponseMessage response = await client.PostAsync(
            _unannotatedPostPath,
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies that a POST to an action without an antiforgery attribute succeeds with a valid token.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task UnannotatedPost_WithAntiforgeryToken_Succeeds()
    {
        using ApplicationWebApplicationFactory factory = CreateFactory();
        using HttpClient client = factory.CreateHttpsClient();

        using HttpResponseMessage tokenResponse = await client.GetAsync(
            "/test/authentication/antiforgery-token",
            TestContext.Current.CancellationToken);
        string token = await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        using HttpResponseMessage response = await client.PostAsync(
            _unannotatedPostPath,
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static ApplicationWebApplicationFactory CreateFactory()
    {
        return ApplicationWebApplicationFactory.CreateAllowingAnonymousAccess(new Dictionary<string, string?>());
    }
}
