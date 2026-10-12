using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.Accessors;
using ProjectTemplate.Web.Authentication.Claims;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Covers the missing-context, unauthenticated, claim-fallback, and active-trace branches of
/// <c>HttpContextApplicationAuditContextAccessor</c>.
/// </summary>
public sealed class HttpContextApplicationAuditContextAccessorBranchCoverageTests
{
    [Fact]
    public void Current_NoHttpContext_ReturnsUnknownActorWithoutCorrelation()
    {
        var accessor = new HttpContextApplicationAuditContextAccessor(
            new HttpContextAccessor { HttpContext = null });

        ApplicationAuditContext context = accessor.Current;

        Assert.Equal("Unknown", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Unknown, context.ActorType);
        Assert.Equal("Unknown", context.ActorDisplayName);
        Assert.Null(context.CorrelationId);
    }

    [Fact]
    public void Current_AnonymousRequestWithoutRemoteIp_ReturnsUnknownActorWithTraceIdentifier()
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = "anonymous-request" };

        ApplicationAuditContext context = CreateAccessor(httpContext).Current;

        Assert.Equal("Unknown", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Unknown, context.ActorType);
        Assert.Equal("anonymous-request", context.CorrelationId);
    }

    [Fact]
    public void Current_UnauthenticatedUserWithSubjectClaim_IgnoresClaimAndUsesRemoteIp()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApplicationClaimTypes.Subject, "not-signed-in")])),
            TraceIdentifier = "unauthenticated-request"
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");

        ApplicationAuditContext context = CreateAccessor(httpContext).Current;

        Assert.Equal("198.51.100.7", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Network, context.ActorType);
        Assert.Equal("Remote IP: 198.51.100.7", context.ActorDisplayName);
        Assert.Equal("unauthenticated-request", context.CorrelationId);
    }

    [Fact]
    public void Current_AuthenticatedUserWithOnlyNameIdentifier_UsesTrimmedNameIdentifier()
    {
        ApplicationAuditContext context = CreateAccessor(
            CreateAuthenticatedContext(new Claim(ClaimTypes.NameIdentifier, " name-identifier-1 "))).Current;

        Assert.Equal("name-identifier-1", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Human, context.ActorType);
    }

    [Fact]
    public void Current_AuthenticatedUserWithBlankSubjectClaims_FallsBackToNameIdentifier()
    {
        ApplicationAuditContext context = CreateAccessor(
            CreateAuthenticatedContext(
                new Claim(ApplicationClaimTypes.Subject, " "),
                new Claim("sub", " "),
                new Claim(ClaimTypes.NameIdentifier, "name-identifier-2"))).Current;

        Assert.Equal("name-identifier-2", context.ActorId);
        Assert.Equal(ApplicationAuditActorTypes.Human, context.ActorType);
    }

    [Fact]
    public void Current_ActiveActivity_RecordsTraceAndSpanForHumanAndNetworkActors()
    {
        DefaultHttpContext anonymousContext = new();
        anonymousContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.8");

        using Activity activity = new("AuditContextAccessorBranchTest");
        activity.Start();

        try
        {
            ApplicationAuditContext human = CreateAccessor(
                CreateAuthenticatedContext(new Claim(ApplicationClaimTypes.Subject, "user-with-trace"))).Current;
            ApplicationAuditContext network = CreateAccessor(anonymousContext).Current;

            Assert.Equal(activity.TraceId.ToString(), human.TraceId);
            Assert.Equal(activity.SpanId.ToString(), human.SpanId);
            Assert.Equal(ApplicationAuditActorTypes.Network, network.ActorType);
            Assert.Equal(activity.TraceId.ToString(), network.TraceId);
            Assert.Equal(activity.SpanId.ToString(), network.SpanId);
        }
        finally
        {
            activity.Stop();
        }
    }

    private static HttpContextApplicationAuditContextAccessor CreateAccessor(DefaultHttpContext httpContext)
    {
        return new HttpContextApplicationAuditContextAccessor(
            new HttpContextAccessor { HttpContext = httpContext });
    }

    private static DefaultHttpContext CreateAuthenticatedContext(params Claim[] claims)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
            TraceIdentifier = "authenticated-request"
        };
    }
}
