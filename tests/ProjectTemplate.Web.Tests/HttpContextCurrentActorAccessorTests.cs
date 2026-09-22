using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectTemplate.Web.Accessors;
using ProjectTemplate.Web.Authentication.Claims;

namespace ProjectTemplate.Web.Tests;

public sealed class HttpContextCurrentActorAccessorTests
{
    [Fact]
    public void CurrentActor_AuthenticatedUserWithSubjectClaim_ReturnsSubject()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(new Claim("sub", "user-123")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Subject: user-123", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserWithOnlyNormalizedSubjectClaim_ReturnsSubject()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(new Claim(ApplicationClaimTypes.Subject, "user-789")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Subject: user-789", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserWithNormalizedAndProviderSubjectClaims_PrefersNormalizedSubject()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(
                new Claim("sub", "provider-subject"),
                new Claim(ApplicationClaimTypes.Subject, "user-789")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Subject: user-789", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserWithWhitespaceSubjectClaim_TrimsSubject()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(new Claim("sub", "  user-123  ")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Subject: user-123", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserMissingSubjectClaim_UsesNameIdentifierFallback()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(new Claim(ClaimTypes.NameIdentifier, "user-456")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Name Identifier: user-456", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserWithWhitespaceSubjectClaim_UsesNameIdentifierFallback()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(
                new Claim("sub", "   "),
                new Claim(ClaimTypes.NameIdentifier, "user-456")));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Name Identifier: user-456", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AuthenticatedUserWithNoUsableClaims_UsesRemoteIpAddress()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAuthenticatedPrincipal(
                new Claim("sub", "   "),
                new Claim(ClaimTypes.NameIdentifier, "   ")),
            IPAddress.Parse("203.0.113.10"));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Remote IP: 203.0.113.10", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AnonymousUserWithSubjectClaim_UsesRemoteIpAddress()
    {
        DefaultHttpContext httpContext = CreateHttpContext(
            CreateAnonymousPrincipal(new Claim("sub", "anonymous-claim")),
            IPAddress.Parse("203.0.113.20"));

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Remote IP: 203.0.113.20", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_AnonymousRequestWithoutRemoteIpAddress_ReturnsUnknown()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        HttpContextCurrentActorAccessor accessor = CreateAccessor(httpContext);

        Assert.Equal("Unknown", accessor.CurrentActor);
    }

    [Fact]
    public void CurrentActor_MissingHttpContext_ReturnsUnknown()
    {
        HttpContextCurrentActorAccessor accessor = CreateAccessor(null);

        Assert.Equal("Unknown", accessor.CurrentActor);
    }

    private static HttpContextCurrentActorAccessor CreateAccessor(HttpContext? httpContext)
    {
        return new HttpContextCurrentActorAccessor(
            new HttpContextAccessor
            {
                HttpContext = httpContext
            });
    }

    private static DefaultHttpContext CreateHttpContext(
        ClaimsPrincipal? user = null,
        IPAddress? remoteIpAddress = null)
    {
        DefaultHttpContext httpContext = new();

        if (user is not null)
        {
            httpContext.User = user;
        }

        if (remoteIpAddress is not null)
        {
            httpContext.Connection.RemoteIpAddress = remoteIpAddress;
        }

        return httpContext;
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static ClaimsPrincipal CreateAnonymousPrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }
}
