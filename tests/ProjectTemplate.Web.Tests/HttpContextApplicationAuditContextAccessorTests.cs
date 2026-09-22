using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.Accessors;
using ProjectTemplate.Web.Authentication.Claims;

namespace ProjectTemplate.Web.Tests;

public sealed class HttpContextApplicationAuditContextAccessorTests
{
    [Fact]
    public void Current_AuthenticatedUserWithOnlyNormalizedSubjectClaim_UsesSubject()
    {
        ApplicationAuditContext context = CreateAccessor(
            CreateAuthenticatedPrincipal(new Claim(ApplicationClaimTypes.Subject, "user-789"))).Current;

        Assert.Equal("user-789", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Human, context.ActorType);
    }

    [Fact]
    public void Current_AuthenticatedUserWithNormalizedAndProviderSubjectClaims_PrefersNormalizedSubject()
    {
        ApplicationAuditContext context = CreateAccessor(
            CreateAuthenticatedPrincipal(
                new Claim("sub", "provider-subject"),
                new Claim(ApplicationClaimTypes.Subject, "user-789"))).Current;

        Assert.Equal("user-789", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Human, context.ActorType);
    }

    [Fact]
    public void Current_AuthenticatedUserWithOnlyProviderSubjectClaim_UsesProviderSubject()
    {
        ApplicationAuditContext context = CreateAccessor(
            CreateAuthenticatedPrincipal(new Claim("sub", "provider-subject"))).Current;

        Assert.Equal("provider-subject", context.ActorId);
    }

    [Fact]
    public void Current_AuthenticatedUserWithoutSubjectClaims_UsesRemoteIpAddress()
    {
        ApplicationAuditContext context = CreateAccessor(CreateAuthenticatedPrincipal()).Current;

        Assert.Equal("192.0.2.10", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Network, context.ActorType);
    }

    private static HttpContextApplicationAuditContextAccessor CreateAccessor(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        return new HttpContextApplicationAuditContextAccessor(
            new HttpContextAccessor { HttpContext = httpContext });
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
