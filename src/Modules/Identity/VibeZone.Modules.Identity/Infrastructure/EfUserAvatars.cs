using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Identity.Domain;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>Identity's side of the Media contract for account pictures.</summary>
internal sealed class EfUserAvatars(AppDbContext db, IEnumerable<IPublicProfileListener> listeners, TimeProvider time) : IUserAvatars
{
    public async Task StageAvatarAsync(Guid userId, Guid? imageId, CancellationToken cancellationToken)
    {
        var profile = await db.Set<UserProfile>().FindAsync([userId], cancellationToken) ?? throw new InvalidOperationException($"Profile {userId} not found.");
        var user = await db.Set<AppUser>().FindAsync([userId], cancellationToken) ?? throw new InvalidOperationException($"User {userId} not found.");
        profile.SetAvatar(imageId, time.GetUtcNow());
        foreach (var listener in listeners)
        {
            await listener.StageProfileChangedAsync(new PublicProfile(userId, user.UserName ?? string.Empty, profile.DisplayName, profile.AvatarId), cancellationToken);
        }
    }
}
