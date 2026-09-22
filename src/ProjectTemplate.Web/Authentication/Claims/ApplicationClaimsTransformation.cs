using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Authentication.Options;

namespace ProjectTemplate.Web.Authentication.Claims;

/// <summary>
/// Normalizes provider-specific claims into application-owned claim names.
/// </summary>
/// <param name="authenticationOptionsAccessor">The application authentication options accessor.</param>
public sealed class ApplicationClaimsTransformation(
    IOptions<ApplicationAuthenticationOptions> authenticationOptionsAccessor) : IClaimsTransformation
{
    private readonly IOptions<ApplicationAuthenticationOptions> _authenticationOptionsAccessor =
        authenticationOptionsAccessor ?? throw new ArgumentNullException(nameof(authenticationOptionsAccessor));

    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        ApplicationClaimsTransformationOptions options =
            _authenticationOptionsAccessor.Value.ClaimsTransformation;

        if (!options.Enabled)
        {
            return Task.FromResult(principal);
        }

        // Transformation may run more than once per request, and the incoming principal can be shared with other
        // components, so normalize copies instead of editing the caller's principal in place.
        var transformed = new ClaimsPrincipal();

        foreach (ClaimsIdentity source in principal.Identities)
        {
            ClaimsIdentity identity = source.Clone();
            ApplicationClaimMappingOptions mappings = ResolveMappings(options, identity.AuthenticationType);

            NormalizeClaim(identity, ApplicationClaimTypes.Subject, mappings.Subject, options.RemoveOriginalClaims);
            NormalizeClaim(identity, ApplicationClaimTypes.Name, mappings.Name, options.RemoveOriginalClaims);
            NormalizeClaim(identity, ApplicationClaimTypes.Email, mappings.Email, options.RemoveOriginalClaims);
            NormalizeClaim(identity, ApplicationClaimTypes.Role, mappings.Role, options.RemoveOriginalClaims);
            NormalizeClaim(identity, ApplicationClaimTypes.Group, mappings.Group, options.RemoveOriginalClaims);
            NormalizeClaim(identity, ApplicationClaimTypes.Permission, mappings.Permission, options.RemoveOriginalClaims);

            transformed.AddIdentity(WithNormalizedNameAndRoleClaimTypes(identity));
        }

        return Task.FromResult(transformed);
    }

    // Points Identity.Name, User.IsInRole, and [Authorize(Roles = "...")] at application:name and
    // application:role, so they keep working once RemoveOriginalClaims strips the provider claims. The original
    // claim type is kept only when the identity still carries it and has no normalized equivalent (for example,
    // when the provider's type is not in the configured mappings).
    private static ClaimsIdentity WithNormalizedNameAndRoleClaimTypes(ClaimsIdentity identity)
    {
        string nameClaimType = ResolveClaimType(identity, ApplicationClaimTypes.Name, identity.NameClaimType);
        string roleClaimType = ResolveClaimType(identity, ApplicationClaimTypes.Role, identity.RoleClaimType);

        return string.Equals(nameClaimType, identity.NameClaimType, StringComparison.Ordinal)
            && string.Equals(roleClaimType, identity.RoleClaimType, StringComparison.Ordinal)
            ? identity
            : new ClaimsIdentity(identity.Claims, identity.AuthenticationType, nameClaimType, roleClaimType)
            {
                Actor = identity.Actor,
                BootstrapContext = identity.BootstrapContext,
                Label = identity.Label
            };
    }

    private static string ResolveClaimType(ClaimsIdentity identity, string normalizedClaimType, string currentClaimType)
    {
        bool hasNormalized = identity.HasClaim(claim =>
            string.Equals(claim.Type, normalizedClaimType, StringComparison.OrdinalIgnoreCase));
        bool hasCurrent = identity.HasClaim(claim =>
            string.Equals(claim.Type, currentClaimType, StringComparison.OrdinalIgnoreCase));

        return hasNormalized || !hasCurrent ? normalizedClaimType : currentClaimType;
    }

    private static ApplicationClaimMappingOptions ResolveMappings(
        ApplicationClaimsTransformationOptions options,
        string? authenticationType)
    {
        return !string.IsNullOrWhiteSpace(authenticationType)
            && options.ProviderMappings.TryGetValue(authenticationType, out ApplicationClaimMappingOptions? providerMappings)
            ? providerMappings
            : options.DefaultMappings;
    }

    private static void NormalizeClaim(
        ClaimsIdentity identity,
        string normalizedClaimType,
        IEnumerable<string> sourceClaimTypes,
        bool removeOriginalClaims)
    {
        var sourceTypes = sourceClaimTypes
            .Where(claimType => !string.IsNullOrWhiteSpace(claimType))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sourceTypes.Count == 0)
        {
            return;
        }

        var sourceClaims = identity.Claims
            .Where(claim => sourceTypes.Contains(claim.Type))
            .ToList();

        if (sourceClaims.Count == 0)
        {
            return;
        }

        foreach (Claim sourceClaim in sourceClaims)
        {
            if (!HasClaim(identity, normalizedClaimType, sourceClaim.Value))
            {
                identity.AddClaim(new Claim(
                    normalizedClaimType,
                    sourceClaim.Value,
                    sourceClaim.ValueType,
                    sourceClaim.Issuer,
                    sourceClaim.OriginalIssuer));
            }
        }

        if (!removeOriginalClaims)
        {
            return;
        }

        foreach (Claim sourceClaim in sourceClaims)
        {
            if (!string.Equals(sourceClaim.Type, normalizedClaimType, StringComparison.OrdinalIgnoreCase))
            {
                identity.RemoveClaim(sourceClaim);
            }
        }
    }

    private static bool HasClaim(
        ClaimsIdentity identity,
        string claimType,
        string claimValue)
    {
        return identity.Claims.Any(claim =>
            string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(claim.Value, claimValue, StringComparison.Ordinal));
    }
}
