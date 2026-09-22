using System.Diagnostics;
using System.Security.Claims;
using ProjectTemplate.Infrastructure.Data.Auditing;
using ProjectTemplate.Web.Authentication.Claims;

namespace ProjectTemplate.Web.Accessors;

/// <summary>
/// Resolves structured application audit context from the active HTTP request.
/// </summary>
public sealed class HttpContextApplicationAuditContextAccessor(
    IHttpContextAccessor httpContextAccessor)
    : IApplicationAuditContextAccessor
{
    private const string _subjectClaimType = "sub";

    public ApplicationAuditContext Current
    {
        get
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;
            ClaimsPrincipal? user = httpContext?.User;

            // Prefer the normalized application claim: claims transformation may remove the provider claims.
            string? subject = GetAuthenticatedClaim(user, ApplicationClaimTypes.Subject)
                ?? GetAuthenticatedClaim(user, _subjectClaimType)
                ?? GetAuthenticatedClaim(user, ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                return new ApplicationAuditContext(
                    subject,
                    ApplicationAuditActorTypes.Human,
                    subject,
                    correlationId: httpContext?.TraceIdentifier,
                    traceId: Activity.Current?.TraceId.ToString(),
                    spanId: Activity.Current?.SpanId.ToString());
            }

            string? remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString();

            return !string.IsNullOrWhiteSpace(remoteIp)
                ? new ApplicationAuditContext(
                    remoteIp,
                    ApplicationAuditActorTypes.Network,
                    $"Remote IP: {remoteIp}",
                    correlationId: httpContext?.TraceIdentifier,
                    traceId: Activity.Current?.TraceId.ToString(),
                    spanId: Activity.Current?.SpanId.ToString())
                : new ApplicationAuditContext(
                "Unknown",
                ApplicationAuditActorTypes.Unknown,
                "Unknown",
                correlationId: httpContext?.TraceIdentifier,
                traceId: Activity.Current?.TraceId.ToString(),
                spanId: Activity.Current?.SpanId.ToString());
        }
    }

    private static string? GetAuthenticatedClaim(
        ClaimsPrincipal? user,
        string claimType)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? value = user.FindFirst(claimType)?.Value?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
