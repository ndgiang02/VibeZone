namespace VibeZone.Modules.Playlists.Contracts;

public sealed record PlaylistInfo(Guid PlaylistId, string Title, Guid OwnerUserId, bool IsPublic, bool TakenDown);

/// <summary>Reports and takedowns for the Moderation module. <c>Stage*</c> changes are committed by the caller.</summary>
public interface IPlaylistModeration
{
    Task<PlaylistInfo?> FindAsync(Guid playlistId, CancellationToken cancellationToken);

    /// <summary>Hides the playlist from everyone but its owner and moderators. False if it doesn't exist.</summary>
    Task<bool> StageTakedownAsync(Guid playlistId, CancellationToken cancellationToken);

    Task<bool> StageRestoreAsync(Guid playlistId, CancellationToken cancellationToken);
}
