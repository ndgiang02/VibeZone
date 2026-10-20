using VibeZone.Modules.Catalog.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

/// <summary>Albums: owner-managed groupings of the artist's own tracks, in order.</summary>
public sealed class AlbumService(ICatalogRepository repository, TimeProvider time)
{
    private static readonly DomainError NotFound = DomainError.NotFound("album_not_found", "Album not found.");

    public async Task<Result<AlbumDto>> CreateAsync(Guid userId, SaveAlbumRequest request, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistByUserAsync(userId, cancellationToken);
        if (artist is null)
        {
            return DomainError.Forbidden("artist_required", "Create an artist profile before adding albums.");
        }

        var (title, error) = Validate(request);
        if (error is not null)
        {
            return error;
        }

        var album = Album.Create(artist.Id, title, request.Kind, request.Description, request.ReleaseDate, request.Visibility, time.GetUtcNow());
        repository.Add(album);
        await repository.SaveChangesAsync(cancellationToken);
        return album.ToDto(artist, [], isOwner: true);
    }

    public async Task<Result<AlbumDetailDto>> GetAsync(Guid albumId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await repository.FindAlbumAsync(albumId, cancellationToken);
        var isOwner = row is not null && viewerUserId == row.Artist.UserId;
        if (row is null || !row.Album.IsVisibleTo(isOwner, viewerIsModerator))
        {
            return NotFound;
        }

        var tracks = VisibleTracks(await repository.ListAlbumTracksAsync([albumId], cancellationToken), isOwner, viewerIsModerator);
        return new AlbumDetailDto(row.Album.ToDto(row.Artist, tracks, isOwner), tracks.Select(t => t.ToDto(isOwner)).ToList());
    }

    public async Task<Result<AlbumDto>> UpdateAsync(Guid userId, Guid albumId, SaveAlbumRequest request, CancellationToken cancellationToken)
    {
        var row = await FindOwnedAsync(userId, albumId, cancellationToken);
        if (row is null)
        {
            return NotFound;
        }

        var (title, error) = Validate(request);
        if (error is not null)
        {
            return error;
        }

        row.Album.Update(title, request.Kind, request.Description, request.ReleaseDate, request.Visibility, time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        var tracks = VisibleTracks(await repository.ListAlbumTracksAsync([albumId], cancellationToken), true, false);
        return row.Album.ToDto(row.Artist, tracks, isOwner: true);
    }

    /// <summary>Soft-deletes the album; its tracks stay and simply no longer belong to an album.</summary>
    public async Task<Result> DeleteAsync(Guid userId, Guid albumId, CancellationToken cancellationToken)
    {
        var row = await FindOwnedAsync(userId, albumId, cancellationToken);
        if (row is null)
        {
            return Result.Failure(NotFound);
        }

        var now = time.GetUtcNow();
        foreach (var track in (await repository.ListArtistTrackEntitiesAsync(row.Artist.Id, cancellationToken)).Where(t => t.AlbumId == albumId))
        {
            track.SetAlbum(null, null, now);
        }

        row.Album.Delete(now);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Replaces the tracklist: the given tracks (the artist's own) in this order; tracks left out leave the album.</summary>
    public async Task<Result<AlbumDetailDto>> SetTracksAsync(Guid userId, Guid albumId, SetAlbumTracksRequest request, CancellationToken cancellationToken)
    {
        var row = await FindOwnedAsync(userId, albumId, cancellationToken);
        if (row is null)
        {
            return NotFound;
        }

        var ids = request.TrackIds ?? [];
        if (ids.Count > Album.MaxTracks || ids.Distinct().Count() != ids.Count)
        {
            return DomainError.Validation("album_tracks_invalid", $"Use at most {Album.MaxTracks} different tracks.", "trackIds");
        }

        var owned = (await repository.ListArtistTrackEntitiesAsync(row.Artist.Id, cancellationToken)).ToDictionary(t => t.Id);
        if (ids.Any(id => !owned.ContainsKey(id)))
        {
            return DomainError.Validation("album_tracks_invalid", "Albums can only contain your own tracks.", "trackIds");
        }

        var now = time.GetUtcNow();
        foreach (var track in owned.Values.Where(t => t.AlbumId == albumId && !ids.Contains(t.Id)))
        {
            track.SetAlbum(null, null, now);
        }

        for (var i = 0; i < ids.Count; i++)
        {
            owned[ids[i]].SetAlbum(albumId, i + 1, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await GetAsync(albumId, userId, false, cancellationToken);
    }

    public async Task<IReadOnlyList<AlbumDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistByUserAsync(userId, cancellationToken);
        return artist is null ? [] : await ListForArtistAsync(artist, isOwner: true, viewerIsModerator: false, cancellationToken);
    }

    /// <summary>Albums of an artist as the viewer may list them (owner: all; others: public only).</summary>
    public async Task<IReadOnlyList<AlbumDto>> ListForArtistAsync(Artist artist, bool isOwner, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var albums = (await repository.ListArtistAlbumsAsync(artist.Id, cancellationToken)).Where(a => a.IsListedFor(isOwner)).ToList();
        var tracks = (await repository.ListAlbumTracksAsync(albums.Select(a => a.Id).ToList(), cancellationToken)).ToLookup(t => t.Track.AlbumId);
        return albums.Select(a => a.ToDto(artist, VisibleTracks(tracks[a.Id], isOwner, viewerIsModerator), isOwner)).ToList();
    }

    /// <summary>Maps albums (of any artists) for list views such as search, counting the viewer's visible tracks.</summary>
    public async Task<IReadOnlyList<AlbumDto>> ToDtosAsync(IReadOnlyList<AlbumRow> rows, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var tracks = (await repository.ListAlbumTracksAsync(rows.Select(r => r.Album.Id).ToList(), cancellationToken)).ToLookup(t => t.Track.AlbumId);
        return rows.Select(r =>
        {
            var isOwner = viewerUserId == r.Artist.UserId;
            return r.Album.ToDto(r.Artist, VisibleTracks(tracks[r.Album.Id], isOwner, false), isOwner);
        }).ToList();
    }

    private static List<TrackRow> VisibleTracks(IEnumerable<TrackRow> tracks, bool isOwner, bool viewerIsModerator) =>
        tracks.Where(t => t.Track.IsVisibleTo(isOwner, viewerIsModerator)).OrderBy(t => t.Track.TrackNumber).ToList();

    private async Task<AlbumRow?> FindOwnedAsync(Guid userId, Guid albumId, CancellationToken cancellationToken)
    {
        var row = await repository.FindAlbumAsync(albumId, cancellationToken);
        return row is not null && row.Artist.UserId == userId && row.Album.DeletedAt is null ? row : null;
    }

    private static (string Title, DomainError? Error) Validate(SaveAlbumRequest request)
    {
        var title = Track.NormalizeTitle(request.Title);
        if (Album.Validate(title, request.Description) is { } invalid)
        {
            return (title, invalid);
        }

        if (!Enum.IsDefined(request.Kind))
        {
            return (title, DomainError.Validation("album_kind_invalid", "Unknown album kind.", "kind"));
        }

        return Enum.IsDefined(request.Visibility)
            ? (title, null)
            : (title, DomainError.Validation("track_visibility_invalid", "Unknown visibility.", "visibility"));
    }
}
