using System.Security.Claims;
using ProjectTemplate.Web.Authentication.Claims;
using ProjectTemplate.Web.Authentication.Options;

namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Provides unit tests for application claims transformation and normalization behavior.
/// </summary>
public sealed class ClaimsTransformationTests
{
    /// <summary>
    /// Verifies that OpenID Connect-style claims are normalized into application-owned claim types.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_NormalizesOidcStyleClaims()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation();

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123"),
                new Claim("name", "Test User"),
                new Claim("email", "test.user@example.test"),
                new Claim("roles", "Administrator"),
                new Claim("groups", "Developers"),
                new Claim("scope", "application.read")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Name &&
            claim.Value == "Test User");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Email &&
            claim.Value == "test.user@example.test");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Role &&
            claim.Value == "Administrator");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Group &&
            claim.Value == "Developers");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Permission &&
            claim.Value == "application.read");
    }

    /// <summary>
    /// Verifies that SAML-style claims are normalized into application-owned claim types.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_NormalizesSamlStyleClaims()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation();

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "Saml2",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "saml-user-123"),
                new Claim(ClaimTypes.Name, "Saml Test User"),
                new Claim(ClaimTypes.Email, "saml.user@example.test"),
                new Claim(ClaimTypes.Role, "Manager"),
                new Claim("memberOf", "Operations"),
                new Claim("permission", "application.write")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "saml-user-123");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Name &&
            claim.Value == "Saml Test User");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Email &&
            claim.Value == "saml.user@example.test");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Role &&
            claim.Value == "Manager");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Group &&
            claim.Value == "Operations");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Permission &&
            claim.Value == "application.write");
    }

    /// <summary>
    /// Verifies that original provider claims are preserved by default.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_PreservesOriginalClaimsByDefault()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation();

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == "sub" &&
            claim.Value == "user-123");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");
    }

    /// <summary>
    /// Verifies that original provider claims are removed when configured explicitly.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_RemovesOriginalClaims_WhenConfigured()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation(new ApplicationClaimsTransformationOptions
        {
            Enabled = true,
            RemoveOriginalClaims = true
        });

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.DoesNotContain(transformed.Claims, claim =>
            claim.Type == "sub" &&
            claim.Value == "user-123");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");
    }

    /// <summary>
    /// Verifies that claims are not normalized when claims transformation is disabled.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_DoesNotAddNormalizedClaims_WhenDisabled()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation(new ApplicationClaimsTransformationOptions
        {
            Enabled = false
        });

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == "sub" &&
            claim.Value == "user-123");

        Assert.DoesNotContain(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");
    }

    /// <summary>
    /// Verifies that provider-specific mappings override the default mappings for the matching authentication type.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_UsesProviderSpecificMappings_WhenConfigured()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation(new ApplicationClaimsTransformationOptions
        {
            Enabled = true,
            ProviderMappings =
            {
                ["GitHub"] = new ApplicationClaimMappingOptions
                {
                    Subject = [ "github_id" ],
                    Name = [ "github_name" ],
                    Email = [ "github_email" ],
                    Role = [ "github_role" ],
                    Group = [ "github_group" ],
                    Permission = [ "github_permission" ]
                }
            }
        });

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "GitHub",
            claims:
            [
                new Claim("github_id", "github-user-123"),
                new Claim("github_name", "GitHub Test User"),
                new Claim("github_email", "github.user@example.test"),
                new Claim("github_role", "Maintainer"),
                new Claim("github_group", "Application Team"),
                new Claim("github_permission", "repository.read")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "github-user-123");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Name &&
            claim.Value == "GitHub Test User");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Email &&
            claim.Value == "github.user@example.test");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Role &&
            claim.Value == "Maintainer");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Group &&
            claim.Value == "Application Team");

        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Permission &&
            claim.Value == "repository.read");
    }

    /// <summary>
    /// Verifies that duplicate normalized claims are not added when transformation runs more than once.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_DoesNotDuplicateNormalizedClaims()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation();

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123")
            ]);

        ClaimsPrincipal transformedOnce = await transformation.TransformAsync(principal);
        ClaimsPrincipal transformedTwice = await transformation.TransformAsync(transformedOnce);

        int normalizedSubjectClaimCount = transformedTwice.Claims.Count(claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");

        Assert.Equal(1, normalizedSubjectClaimCount);
    }

    /// <summary>
    /// Verifies that transformation returns a new principal and leaves the incoming principal unchanged.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_RemoveOriginalClaims_DoesNotMutateIncomingPrincipal()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation(
            new ApplicationClaimsTransformationOptions { RemoveOriginalClaims = true });

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim("sub", "user-123"),
                new Claim("roles", "Administrator")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        Assert.NotSame(principal, transformed);
        Assert.Equal(["sub", "roles"], principal.Claims.Select(claim => claim.Type));
        Assert.DoesNotContain(transformed.Claims, claim => claim.Type == "sub");
        Assert.Contains(transformed.Claims, claim =>
            claim.Type == ApplicationClaimTypes.Subject &&
            claim.Value == "user-123");
    }

    /// <summary>
    /// Verifies that role and name checks resolve against normalized claims after original claims are removed.
    /// </summary>
    [Fact]
    public async Task ClaimsTransformation_RemoveOriginalClaims_KeepsIsInRoleAndNameWorking()
    {
        ApplicationClaimsTransformation transformation = CreateTransformation(
            new ApplicationClaimsTransformationOptions { RemoveOriginalClaims = true });

        ClaimsPrincipal principal = CreatePrincipal(
            authenticationType: "OpenIdConnect",
            claims:
            [
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Role, "Administrator")
            ]);

        ClaimsPrincipal transformed = await transformation.TransformAsync(principal);

        ClaimsIdentity identity = Assert.IsType<ClaimsIdentity>(transformed.Identity);
        Assert.Equal(ApplicationClaimTypes.Role, identity.RoleClaimType);
        Assert.Equal(ApplicationClaimTypes.Name, identity.NameClaimType);
        Assert.True(identity.IsAuthenticated);
        Assert.True(transformed.IsInRole("Administrator"));
        Assert.Equal("Test User", transformed.Identity?.Name);
    }

    private static ApplicationClaimsTransformation CreateTransformation(
        ApplicationClaimsTransformationOptions? claimsTransformationOptions = null)
    {
        ApplicationAuthenticationOptions authenticationOptions = new()
        {
            ClaimsTransformation = claimsTransformationOptions ?? new ApplicationClaimsTransformationOptions()
        };

        return new ApplicationClaimsTransformation(Microsoft.Extensions.Options.Options.Create(authenticationOptions));
    }

    private static ClaimsPrincipal CreatePrincipal(
        string authenticationType,
        IEnumerable<Claim> claims)
    {
        ClaimsIdentity identity = new(claims, authenticationType);

        return new ClaimsPrincipal(identity);
    }
}
