namespace VibeZone.Modules.Identity.Contracts;

/// <summary>
/// An account's public identity. The account is its single source: other modules (the artist profile) show
/// copies of it and are told when it changes (ADR-0013).
/// </summary>
/// <param name="AvatarId">Version of the current account picture (see <see cref="UserAvatarPaths"/>), or null.</param>
public sealed record PublicProfile(Guid UserId, string Handle, string DisplayName, Guid? AvatarId);

/// <summary>
/// Implemented by modules that keep a copy of <see cref="PublicProfile"/>. Called when the display name or picture
/// changes; only records the change in the shared unit of work — Identity commits it in the same transaction.
/// </summary>
public interface IPublicProfileListener
{
    Task StageProfileChangedAsync(PublicProfile profile, CancellationToken cancellationToken);
}
