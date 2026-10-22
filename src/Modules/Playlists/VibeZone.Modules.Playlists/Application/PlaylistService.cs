using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Playlists.Contracts;
using VibeZone.Modules.Playlists.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Playlists.Application;

public sealed record PlaylistOwnerDto(Guid Id, string DisplayName, string Handle, string? AvatarUrl);

/// <param name="CoverUrls">Up to four track covers (in playlist order) for a mosaic; empty if none.</param>
/// <param name="ContainsTrack">Only on <c>GET /me/playlists?containsTrack=</c>: whether that track is in it.</param>
public sealed record PlaylistDto(
    Guid Id,
    string Title,
    string? Description,
    PlaylistVisibility Visibility,
    int TrackCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PlaylistOwnerDto Owner,
    IReadOnlyList<string> CoverUrls,
    bool IsOwner,
    bool? ContainsTrack,
    bool TakenDown);

public sealed record SavePlaylistRequest(string? Title, string? Description, PlaylistVisibility Visibility);

public sealed record AddPlaylistTrackRequest(Guid TrackId);

public sealed record ReorderPlaylistRequest(IReadOnlyList<Guid>? TrackIds);

/// <summary>Persistence port for the Playlists module.</summary>
public interface IPlaylistRepository
{
    /// <summary>With its tracks, tracked for update.</summary>
    Task<Playlist?> FindAsync(Guid playlistId, CancellationToken cancellationToken);

    /// <summary>Newest-updated first.</summary>
    Task<Paged<Playlist>> PageByOwnerAsync(Guid ownerUserId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Public playlists with at least one track: matching <paramref name="query"/> (accent-insensitive) or newest.</summary>
    Task<Paged<Playlist>> PagePublicAsync(string? query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The first <paramref name="perPlaylist"/> track ids of each playlist, in order.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> FirstTrackIdsAsync(IReadOnlyCollection<Guid> playlistIds, int perPlaylist, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> PlaylistsContainingAsync(IReadOnlyCollection<Guid> playlistIds, Guid trackId, CancellationToken cancellationToken);

    void Add(Playlist playlist);

    void Remove(Playlist playlist);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// IPlaylistReader on its own (only the repository): Catalog depends on it, and PlaylistService depends on
/// Catalog's ITrackCatalog, so implementing it there would make a dependency cycle.
/// </summary>
public sealed class PlaylistReader(IPlaylistRepository repository) : IPlaylistReader
{
    public async Task<IReadOnlyList<Guid>?> GetTrackIdsAsync(Guid playlistId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var playlist = await repository.FindAsync(playlistId, cancellationToken);
        return playlist is not null && playlist.IsVisibleTo(viewerUserId, viewerIsModerator)
            ? playlist.Tracks.OrderBy(t => t.Position).Select(t => t.TrackId).ToList()
            : null;
    }
}

public sealed class PlaylistService(IPlaylistRepository repository, ITrackCatalog tracks, IUserDirectory users, TimeProvider time)
{
    private const int MosaicSize = 4;
    private static readonly DomainError NotFound = DomainError.NotFound("playlist_not_found", "Playlist not found.");

    public async Task<Result<PlaylistDto>> CreateAsync(Guid userId, SavePlaylistRequest request, CancellationToken cancellationToken)
    {
        var title = Playlist.NormalizeTitle(request.Title);
        if (Playlist.Validate(title, request.Description, request.Visibility) is { } invalid)
        {
            return invalid;
        }

        var playlist = Playlist.Create(userId, title, request.Description, request.Visibility, time.GetUtcNow());
        repository.Add(playlist);
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([playlist], userId, null, cancellationToken))[0];
    }

    public async Task<Result<PlaylistDto>> GetAsync(Guid playlistId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var playlist = await repository.FindAsync(playlistId, cancellationToken);
        return playlist is not null && playlist.IsVisibleTo(viewerUserId, viewerIsModerator)
            ? (await ToDtosAsync([playlist], viewerUserId, null, cancellationToken))[0]
            : NotFound;
    }

    public async Task<Result<PlaylistDto>> UpdateAsync(Guid userId, Guid playlistId, SavePlaylistRequest request, CancellationToken cancellationToken)
    {
        if (await FindOwnedAsync(userId, playlistId, cancellationToken) is not { } playlist)
        {
            return NotFound;
        }

        var title = Playlist.NormalizeTitle(request.Title);
        if (Playlist.Validate(title, request.Description, request.Visibility) is { } invalid)
        {
            return invalid;
        }

        playlist.Update(title, request.Description, request.Visibility, time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([playlist], userId, null, cancellationToken))[0];
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid playlistId, CancellationToken cancellationToken)
    {
        if (await FindOwnedAsync(userId, playlistId, cancellationToken) is not { } playlist)
        {
            return Result.Failure(NotFound);
        }

        repository.Remove(playlist);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Adds a track the user can see (their own private ones included). Idempotent.</summary>
    public async Task<Result<PlaylistDto>> AddTrackAsync(Guid userId, Guid playlistId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await FindOwnedAsync(userId, playlistId, cancellationToken) is not { } playlist)
        {
            return NotFound;
        }

        if (!await tracks.CanViewAsync(trackId, userId, false, cancellationToken))
        {
            return DomainError.NotFound("track_not_found", "Track not found.");
        }

        if (playlist.Add(trackId, time.GetUtcNow()) is { } full)
        {
            return full;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([playlist], userId, null, cancellationToken))[0];
    }

    public async Task<Result> RemoveTrackAsync(Guid userId, Guid playlistId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await FindOwnedAsync(userId, playlistId, cancellationToken) is not { } playlist)
        {
            return Result.Failure(NotFound);
        }

        if (playlist.Remove(trackId, time.GetUtcNow()))
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<PlaylistDto>> ReorderAsync(Guid userId, Guid playlistId, ReorderPlaylistRequest request, CancellationToken cancellationToken)
    {
        if (await FindOwnedAsync(userId, playlistId, cancellationToken) is not { } playlist)
        {
            return NotFound;
        }

        if (playlist.Reorder(request.TrackIds ?? [], time.GetUtcNow()) is { } invalid)
        {
            return invalid;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return (await ToDtosAsync([playlist], userId, null, cancellationToken))[0];
    }

    public async Task<Paged<PlaylistDto>> ListMineAsync(Guid userId, PageRequest page, Guid? containsTrack, CancellationToken cancellationToken)
    {
        var playlists = await repository.PageByOwnerAsync(userId, page, cancellationToken);
        var dtos = await ToDtosAsync(playlists.Items, userId, containsTrack, cancellationToken);
        return new Paged<PlaylistDto>(dtos, playlists.Page, playlists.PageSize, playlists.Total);
    }

    public async Task<Paged<PlaylistDto>> ListPublicAsync(string? query, PageRequest page, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var playlists = await repository.PagePublicAsync(string.IsNullOrWhiteSpace(query) ? null : query.Trim(), page, cancellationToken);
        var dtos = await ToDtosAsync(playlists.Items, viewerUserId, null, cancellationToken);
        return new Paged<PlaylistDto>(dtos, playlists.Page, playlists.PageSize, playlists.Total);
    }

    private async Task<Playlist?> FindOwnedAsync(Guid userId, Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await repository.FindAsync(playlistId, cancellationToken);
        return playlist?.OwnerUserId == userId ? playlist : null;
    }

    private async Task<List<PlaylistDto>> ToDtosAsync(IReadOnlyList<Playlist> playlists, Guid? viewerUserId, Guid? containsTrack, CancellationToken cancellationToken)
    {
        var ids = playlists.Select(p => p.Id).ToList();
        // Covers come from the first few tracks; ask for more than four so tracks without covers are skipped.
        var first = await repository.FirstTrackIdsAsync(ids, MosaicSize * 3, cancellationToken);
        var covers = await tracks.GetArtworkUrlsAsync(first.Values.SelectMany(x => x).Distinct().ToList(), cancellationToken);
        var owners = await users.GetAsync(playlists.Select(p => p.OwnerUserId).Distinct().ToList(), cancellationToken);
        var containing = containsTrack is { } trackId ? await repository.PlaylistsContainingAsync(ids, trackId, cancellationToken) : null;

        return playlists.Select(p =>
        {
            var owner = owners.TryGetValue(p.OwnerUserId, out var o)
                ? new PlaylistOwnerDto(o.Id, o.DisplayName, o.Handle, o.AvatarUrl)
                : new PlaylistOwnerDto(p.OwnerUserId, string.Empty, string.Empty, null);
            var mosaic = first.TryGetValue(p.Id, out var trackIds)
                ? trackIds.Where(covers.ContainsKey).Select(id => covers[id]).Distinct().Take(MosaicSize).ToList()
                : [];
            return new PlaylistDto(p.Id, p.Title, p.Description, p.Visibility, p.TrackCount, p.CreatedAt, p.UpdatedAt, owner, mosaic,
                viewerUserId == p.OwnerUserId, containing?.Contains(p.Id), p.TakenDownAt is not null);
        }).ToList();
    }
}
