using Microsoft.AspNetCore.Authorization;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Security;

/// <summary>Named policies for staff endpoints. Anonymous → 401, signed in without the role → 403.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Moderators and admins: report queue, takedown/restore, all-tracks view.</summary>
    public const string Moderator = "moderator";

    /// <summary>Admins only: users and roles, jobs, audit log.</summary>
    public const string Admin = "admin";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Moderator, p => p.RequireAuthenticatedUser().RequireRole(Roles.Moderator, Roles.Admin));
        options.AddPolicy(Admin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));
    }
}

public static class CurrentUserExtensions
{
    public static bool IsModerator(this ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);
}
