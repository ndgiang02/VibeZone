using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Playlists.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

public sealed class TrackService(ICatalogRepository repository, IPlaylistReader playlists, TimeProvider time)
{
    internal static readonly DomainError NotFound = DomainError.NotFound("track_not_found", "Track not found.");

    public async Task<Result<TrackDto>> CreateAsync(Guid userId, SaveTrackRequest request, CancellationToken cancellationToken)
    {
        var staged = await StageCreateAsync(userId, request, cancellationToken);
        if (!staged.IsSuccess)
        {
            return staged.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return staged.Value.ToDto(isOwner: true);
    }

    /// <summary>Validates and adds a Draft track to the unit of work without saving (the caller commits).</summary>
    public async Task<Result<TrackRow>> StageCreateAsync(Guid userId, SaveTrackRequest request, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistByUserAsync(userId, cancellationToken);
        if (artist is null)
        {
            return DomainError.Forbidden("artist_required", "Create an artist profile before adding tracks.");
        }

        var (title, genre, error) = await ValidateAsync(request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var track = Track.Create(artist.Id, title, request.Description, genre?.Id, request.Visibility, time.GetUtcNow());
        repository.Add(track);
        return new TrackRow(track, artist, genre, null);
    }

    public async Task<Result<TrackDto>> GetAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await repository.FindTrackAsync(trackId, cancellationToken);
        var isOwner = row is not null && viewerUserId == row.Artist.UserId;
        return row is not null && row.Track.IsVisibleTo(isOwner, viewerIsModerator) ? row.ToDto(isOwner, viewerIsModerator) : NotFound;
    }

    public async Task<Result<TrackDto>> UpdateAsync(Guid userId, Guid trackId, SaveTrackRequest request, CancellationToken cancellationToken)
    {
        var row = await FindOwnedAsync(userId, trackId, cancellationToken);
        if (row is null)
        {
            return NotFound;
        }

        var (title, genre, error) = await ValidateAsync(request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        row.Track.UpdateMetadata(title, request.Description, genre?.Id, request.Visibility, time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return (row with { Genre = genre }).ToDto(isOwner: true);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        var row = await FindOwnedAsync(userId, trackId, cancellationToken);
        if (row is null)
        {
            return Result.Failure(NotFound);
        }

        row.Track.Delete(time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Paged<TrackDto>> ListMineAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistByUserAsync(userId, cancellationToken);
        return artist is null
            ? new Paged<TrackDto>([], page.Page, page.PageSize, 0)
            : (await repository.PageArtistTracksAsync(artist.Id, page, cancellationToken)).Map(r => r.ToDto(isOwner: true));
    }

    public async Task<Paged<TrackDto>> ListNewReleasesAsync(PageRequest page, Guid? viewerUserId, CancellationToken cancellationToken) =>
        (await repository.PageNewReleasesAsync(page, cancellationToken)).Map(r => r.ToDto(isOwner: viewerUserId == r.Artist.UserId));

    /// <summary>A playlist's tracks in order, leaving out those the viewer can't see (private, deleted, not ready).</summary>
    public async Task<Result<IReadOnlyList<TrackDto>>> ListPlaylistTracksAsync(Guid playlistId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var ids = await playlists.GetTrackIdsAsync(playlistId, viewerUserId, viewerIsModerator, cancellationToken);
        if (ids is null)
        {
            return DomainError.NotFound("playlist_not_found", "Playlist not found.");
        }

        var rows = (await repository.ListTracksAsync(ids, cancellationToken)).ToDictionary(r => r.Track.Id);
        return ids.Where(rows.ContainsKey)
            .Select(id => rows[id])
            .Where(r => r.Track.IsVisibleTo(viewerUserId == r.Artist.UserId, viewerIsModerator))
            .Select(r => r.ToDto(viewerUserId == r.Artist.UserId, viewerIsModerator))
            .ToList();
    }

    private async Task<TrackRow?> FindOwnedAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        var row = await repository.FindTrackAsync(trackId, cancellationToken);
        return row is not null && row.Artist.UserId == userId && row.Track.DeletedAt is null ? row : null;
    }

    private async Task<(string Title, Genre? Genre, DomainError? Error)> ValidateAsync(SaveTrackRequest request, CancellationToken cancellationToken)
    {
        var title = Track.NormalizeTitle(request.Title);
        if (Track.Validate(title, request.Description) is { } invalid)
        {
            return (title, null, invalid);
        }

        if (!Enum.IsDefined(request.Visibility))
        {
            return (title, null, DomainError.Validation("track_visibility_invalid", "Unknown visibility.", "visibility"));
        }

        if (string.IsNullOrWhiteSpace(request.GenreSlug))
        {
            return (title, null, null);
        }

        var genre = await repository.FindGenreAsync(request.GenreSlug.Trim().ToLowerInvariant(), cancellationToken);
        return genre is null
            ? (title, null, DomainError.Validation("genre_unknown", "Unknown genre.", "genreSlug"))
            : (title, genre, null);
    }
}
