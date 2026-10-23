namespace VibeZone.Modules.Playlists.Contracts;

/// <summary>
/// Playlist contents for other modules: Catalog renders a playlist's tracks (it owns the track DTOs), so it
/// asks here which tracks a playlist holds — after the same visibility check as <c>GET /playlists/{id}</c>.
/// </summary>
public interface IPlaylistReader
{
    /// <summary>Track ids in playlist order, or null if the playlist doesn't exist or the viewer can't see it.</summary>
    Task<IReadOnlyList<Guid>?> GetTrackIdsAsync(Guid playlistId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken);
}
