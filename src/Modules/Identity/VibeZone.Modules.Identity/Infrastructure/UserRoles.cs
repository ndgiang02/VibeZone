using Microsoft.AspNetCore.Identity;
using VibeZone.Modules.Identity.Contracts;

namespace VibeZone.Modules.Identity.Infrastructure;

internal sealed class UserRoles(UserManager<AppUser> users, EfUserSessions sessions) : IUserRoles
{
    public async Task GrantAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        if (!await users.IsInRoleAsync(user, role))
        {
            var result = await users.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not grant role '{role}'.");
            }
        }

        // Roles are read from the cached session; evict so the next request sees the new role.
        await sessions.InvalidateUserAsync(userId, cancellationToken);
    }
}
