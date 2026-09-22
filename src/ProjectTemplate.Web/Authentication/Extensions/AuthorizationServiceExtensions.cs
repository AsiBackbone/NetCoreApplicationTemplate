using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ProjectTemplate.Web.Authentication.Options;

namespace ProjectTemplate.Web.Authentication.Extensions;

/// <summary>
/// Provides extension methods for registering application authorization services.
/// </summary>
public static class AuthorizationServiceExtensions
{
    /// <summary>
    /// Adds application authorization services and baseline policies.
    /// </summary>
    /// <param name="services">The service collection to add authorization services to.</param>
    /// <param name="configuration">The application configuration source.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddApplicationAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ApplicationAuthorizationOptions>()
            .Bind(configuration.GetSection(ApplicationAuthorizationOptions.SectionName))
            .Validate(
                authorizationOptions =>
                    !authorizationOptions.RequireAuthenticatedUserByDefault ||
                    configuration.GetValue<bool>($"{ApplicationAuthenticationOptions.SectionName}:Enabled"),
                "ProjectTemplate:Authorization:RequireAuthenticatedUserByDefault cannot be true when ProjectTemplate:Authentication:Enabled is false.")
            .Validate(
                authorizationOptions => !string.IsNullOrWhiteSpace(authorizationOptions.RoleClaimType),
                "ProjectTemplate:Authorization:RoleClaimType is required.")
            .Validate(
                authorizationOptions => !string.IsNullOrWhiteSpace(authorizationOptions.PermissionClaimType),
                "ProjectTemplate:Authorization:PermissionClaimType is required.")
            .Validate(
                authorizationOptions => authorizationOptions.AdministratorRoles.Any(role => !string.IsNullOrWhiteSpace(role)),
                "ProjectTemplate:Authorization:AdministratorRoles must contain at least one non-empty value.")
            .Validate(
                authorizationOptions => authorizationOptions.ManageApplicationPermissions.Any(permission => !string.IsNullOrWhiteSpace(permission)),
                "ProjectTemplate:Authorization:ManageApplicationPermissions must contain at least one non-empty value.")
            .ValidateOnStart();

        _ = services.AddAuthorization();

        // Build every policy from the bound and validated ApplicationAuthorizationOptions instance, so the
        // policies and the validators above always see the same values.
        services
            .AddOptions<AuthorizationOptions>()
            .Configure<IOptions<ApplicationAuthorizationOptions>>((authorizationOptions, applicationAuthorizationOptionsAccessor) =>
            {
                ApplicationAuthorizationOptions applicationAuthorizationOptions = applicationAuthorizationOptionsAccessor.Value;

                authorizationOptions.AddPolicy(
                    ApplicationAuthorizationPolicyNames.AuthenticatedUser,
                    policy => policy.RequireAuthenticatedUser());
                authorizationOptions.AddPolicy(
                    ApplicationAuthorizationPolicyNames.AdministratorRole,
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireClaim(
                            applicationAuthorizationOptions.RoleClaimType,
                            applicationAuthorizationOptions.AdministratorRoles);
                    });
                authorizationOptions.AddPolicy(
                    ApplicationAuthorizationPolicyNames.ManageApplicationPermission,
                    policy =>
                    {
                        policy.RequireAuthenticatedUser();
                        policy.RequireClaim(
                            applicationAuthorizationOptions.PermissionClaimType,
                            applicationAuthorizationOptions.ManageApplicationPermissions);
                    });

                authorizationOptions.FallbackPolicy = applicationAuthorizationOptions.RequireAuthenticatedUserByDefault
                    ? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
                    : null;
            });

        return services;
    }
}

/// <summary>
/// Provides application authorization policy names.
/// </summary>
public static class ApplicationAuthorizationPolicyNames
{
    /// <summary>
    /// Policy requiring the current user to be authenticated.
    /// </summary>
    public const string AuthenticatedUser = "application.AuthenticatedUser";

    /// <summary>
    /// Policy defining the administrator role.
    /// </summary>
    public const string AdministratorRole = "application.Role.Administrator";

    /// <summary>
    /// Policy defining the manage application permission.
    /// </summary>
    public const string ManageApplicationPermission = "application.Permission.ManageApplication";
}
