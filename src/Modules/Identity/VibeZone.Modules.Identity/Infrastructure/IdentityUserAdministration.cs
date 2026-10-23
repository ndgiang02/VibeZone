using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Infrastructure;

internal sealed class IdentityUserAdministration(AppDbContext db, UserManager<AppUser> userManager, EfUserSessions sessions) : IUserAdministration
{
    public async Task<Paged<AdminUserDto>> PageAsync(string? query, string? role, PageRequest page, CancellationToken cancellationToken)
    {
        var users = from u in db.Set<AppUser>().AsNoTracking()
                    join p in db.Set<UserProfile>().AsNoTracking() on u.Id equals p.UserId
                    select new { User = u, Profile = p };
        if (query is not null)
        {
            var pattern = "%" + query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            users = users.Where(x => EF.Functions.ILike(x.User.UserName!, pattern) || EF.Functions.ILike(x.User.Email!, pattern) || EF.Functions.ILike(x.Profile.DisplayName, pattern));
        }

        if (role is not null)
        {
            var roleId = await db.Set<AppRole>().Where(r => r.Name == role).Select(r => r.Id).SingleAsync(cancellationToken);
            users = users.Where(x => db.Set<IdentityUserRole<Guid>>().Any(ur => ur.UserId == x.User.Id && ur.RoleId == roleId));
        }

        var total = await users.CountAsync(cancellationToken);
        var rows = total <= page.Skip
            ? []
            : await users.OrderByDescending(x => x.User.CreatedAt).ThenBy(x => x.User.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        var roles = await RolesAsync(rows.Select(r => r.User.Id).ToList(), cancellationToken);
        var items = rows.Select(r => ToDto(r.User, r.Profile, roles.GetValueOrDefault(r.User.Id) ?? [])).ToList();
        return new Paged<AdminUserDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<AdminUserDto?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await (from u in db.Set<AppUser>().AsNoTracking()
                         join p in db.Set<UserProfile>().AsNoTracking() on u.Id equals p.UserId
                         where u.Id == userId
                         select new { User = u, Profile = p }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDto(row.User, row.Profile, await RoleQueries.ForUserAsync(db, userId, cancellationToken));
    }

    public async Task SetRoleAsync(Guid userId, string role, bool member, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var result = member ? await userManager.AddToRoleAsync(user, role) : await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not change role '{role}'.");
        }

        // Roles are read from the cached session; evict so the next request sees the change.
        await sessions.InvalidateUserAsync(userId, cancellationToken);
    }

    private async Task<Dictionary<Guid, List<string>>> RolesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        (await db.Set<IdentityUserRole<Guid>>().AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Set<AppRole>(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken))
        .GroupBy(x => x.UserId)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).Order().ToList());

    private static AdminUserDto ToDto(AppUser user, UserProfile profile, IReadOnlyList<string> roles) => new(
        user.Id,
        user.UserName ?? string.Empty,
        profile.DisplayName,
        user.Email ?? string.Empty,
        user.EmailConfirmed,
        roles,
        user.CreatedAt,
        profile.AvatarId is { } a ? UserAvatarPaths.For(user.Id, a, "small") : null);
}
