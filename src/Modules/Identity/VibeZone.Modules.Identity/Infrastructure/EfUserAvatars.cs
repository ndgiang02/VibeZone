using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Identity.Domain;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>Identity's side of the Media contract for account pictures.</summary>
internal sealed class EfUserAvatars(AppDbContext db, TimeProvider time) : IUserAvatars
{
    public async Task StageAvatarAsync(Guid userId, Guid? imageId, CancellationToken cancellationToken)
    {
        var profile = await db.Set<UserProfile>().FindAsync([userId], cancellationToken) ?? throw new InvalidOperationException($"Profile {userId} not found.");
        profile.SetAvatar(imageId, time.GetUtcNow());
    }
}
