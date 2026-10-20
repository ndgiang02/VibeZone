namespace VibeZone.Modules.Identity.Contracts;

/// <summary>
/// Account pictures for the Media module (which stores them). <see cref="StageAvatarAsync"/> only records the
/// change in the shared unit of work; the caller commits (ADR-0001).
/// </summary>
public interface IUserAvatars
{
    /// <summary>Points the user's profile at its current picture (an opaque version id), or clears it.</summary>
    Task StageAvatarAsync(Guid userId, Guid? imageId, CancellationToken cancellationToken);
}

/// <summary>Account picture URLs (served by the Media module). The version makes a changed picture a new URL.</summary>
public static class UserAvatarPaths
{
    public static string For(Guid userId, Guid imageId, string size) => $"/api/v1/users/{userId}/avatar/{size}?v={imageId:N}";
}
