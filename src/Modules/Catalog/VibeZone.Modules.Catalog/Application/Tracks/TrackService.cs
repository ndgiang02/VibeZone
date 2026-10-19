using VibeZone.Modules.Catalog.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

public sealed class TrackService(ICatalogRepository repository, TimeProvider time)
{
    public const int MaxListLimit = 50;

    internal static readonly DomainError NotFound = DomainError.NotFound("track_not_found", "Track not found.");

    public async Task<Result<TrackDto>> CreateAsync(Guid userId, SaveTrackRequest request, CancellationToken cancellationToken)
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
        await repository.SaveChangesAsync(cancellationToken);
        return new TrackRow(track, artist, genre).ToDto(isOwner: true);
    }

    public async Task<Result<TrackDto>> GetAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await repository.FindTrackAsync(trackId, cancellationToken);
        var isOwner = row is not null && viewerUserId == row.Artist.UserId;
        return row is not null && row.Track.IsVisibleTo(isOwner, viewerIsModerator) ? row.ToDto(isOwner) : NotFound;
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

    public async Task<IReadOnlyList<TrackDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistByUserAsync(userId, cancellationToken);
        return artist is null
            ? []
            : (await repository.ListArtistTracksAsync(artist.Id, cancellationToken)).Select(r => r.ToDto(isOwner: true)).ToList();
    }

    public async Task<IReadOnlyList<TrackDto>> ListNewReleasesAsync(int limit, Guid? viewerUserId, CancellationToken cancellationToken) =>
        (await repository.ListNewReleasesAsync(Math.Clamp(limit, 1, MaxListLimit), cancellationToken))
            .Select(r => r.ToDto(isOwner: viewerUserId == r.Artist.UserId))
            .ToList();

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
