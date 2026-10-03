using Microsoft.AspNetCore.Authorization;

namespace QrMenu.Infrastructure.Auth;

/// <summary>
/// Who may call what. Three kinds of token exist and each reaches only its own endpoints:
/// owner (restaurantId), kitchen screen (restaurantId + kitchen) and super admin (superAdmin, no restaurantId).
/// </summary>
public static class AuthorizationPolicies
{
    public const string Owner = "Owner";
    public const string Kitchen = "Kitchen";
    public const string SuperAdmin = "SuperAdmin";

    public static void Configure(AuthorizationOptions options)
    {
        // Restaurant owners: tokens that carry a restaurantId — but not kitchen-screen tokens, which carry one too.
        options.AddPolicy(Owner, policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == "restaurantId") && !ctx.User.HasClaim("kitchen", "true")));

        // Kitchen display: a kitchen-screen token or the owner's own token (both are tied to one restaurant).
        options.AddPolicy(Kitchen, policy => policy.RequireAuthenticatedUser().RequireClaim("restaurantId"));

        // Platform administrators: tokens that carry the superAdmin claim (and no restaurantId).
        options.AddPolicy(SuperAdmin, policy => policy.RequireAuthenticatedUser().RequireClaim("superAdmin", "true"));
    }
}
