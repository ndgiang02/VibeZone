using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Identity.Domain;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>Public account details only (no email, roles or security data).</summary>
internal sealed class EfUserDirectory(AppDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        var rows = await (from u in db.Set<AppUser>().AsNoTracking()
                          join p in db.Set<UserProfile>().AsNoTracking() on u.Id equals p.UserId
                          where userIds.Contains(u.Id)
                          select new { u.Id, p.DisplayName, u.UserName, p.AvatarId })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(
            r => r.Id,
            r => new UserSummary(r.Id, r.DisplayName, r.UserName ?? string.Empty, r.AvatarId is { } a ? UserAvatarPaths.For(r.Id, a, "small") : null));
    }

    public Task<PublicProfile?> FindProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        (from u in db.Set<AppUser>().AsNoTracking()
         join p in db.Set<UserProfile>().AsNoTracking() on u.Id equals p.UserId
         where u.Id == userId
         select new PublicProfile(u.Id, u.UserName ?? string.Empty, p.DisplayName, p.AvatarId))
            .SingleOrDefaultAsync(cancellationToken);
}
