using System.Security.Claims;
using ProjectTemplate.Infrastructure.Data;
using ProjectTemplate.Web.Authentication.Claims;

namespace ProjectTemplate.Web.Accessors;

/// <summary>
/// An implementation of <see cref="ICurrentActorAccessor"/> that retrieves the current actor information from the HTTP context.
/// </summary>
/// <param name="httpContextAccessor"></param>
public sealed class HttpContextCurrentActorAccessor(
    IHttpContextAccessor httpContextAccessor)
    : ICurrentActorAccessor
{
    private const string _subjectClaimType = "sub";
    private const string _unknownActor = "Unknown";

    /// <summary>
    /// Accesses the current actor information from the HTTP context. It first attempts to retrieve the authenticated
    /// normalized application subject claim, then the provider subject claim, then falls back to the authenticated name identifier claim, then the remote
    /// IP address. If none are available, it returns "Unknown".
    /// </summary>
    public string CurrentActor
    {
        get
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;

            string? authenticatedActor = GetAuthenticatedActor(httpContext?.User);

            if (!string.IsNullOrWhiteSpace(authenticatedActor))
            {
                return authenticatedActor;
            }

            string? remoteIpAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

            return !string.IsNullOrWhiteSpace(remoteIpAddress)
                ? $"Remote IP: {remoteIpAddress}"
                : _unknownActor;
        }
    }

    private static string? GetAuthenticatedActor(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        // Prefer the normalized application claim: when claims transformation removes the original claims,
        // the provider "sub" and name identifier claims are no longer present.
        string? subject = GetClaimValue(user, ApplicationClaimTypes.Subject)
            ?? GetClaimValue(user, _subjectClaimType);

        if (!string.IsNullOrWhiteSpace(subject))
        {
            return $"Subject: {subject}";
        }

        string? nameIdentifier = GetClaimValue(user, ClaimTypes.NameIdentifier);

        return !string.IsNullOrWhiteSpace(nameIdentifier)
            ? $"Name Identifier: {nameIdentifier}"
            : null;
    }

    private static string? GetClaimValue(ClaimsPrincipal user, string claimType)
    {
        string? value = user.FindFirst(claimType)?.Value?.Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
