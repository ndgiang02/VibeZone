using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Application.Tracks;

public sealed record ArtistRefDto(Guid Id, string Slug, string Name);

public sealed record ArtistDto(Guid Id, string Slug, string Name, string? Bio, DateTimeOffset CreatedAt);

public sealed record TrackDto(
    Guid Id,
    string Title,
    string? Description,
    TrackStatus Status,
    TrackVisibility Visibility,
    int? DurationMs,
    string? ProcessingError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    ArtistRefDto Artist,
    GenreDto? Genre,
    bool IsOwner,
    bool Playable);

public sealed record ArtistProfileDto(ArtistDto Artist, IReadOnlyList<TrackDto> Tracks);

public sealed record CreateArtistRequest(string? Slug, string? Name, string? Bio);

public sealed record SaveTrackRequest(string? Title, string? Description, string? GenreSlug, TrackVisibility Visibility);

/// <summary>A track with the rows needed to render and authorize it.</summary>
public sealed record TrackRow(Track Track, Artist Artist, Genre? Genre);

/// <summary>Persistence port for the Catalog module.</summary>
public interface ICatalogRepository
{
    Task<Artist?> FindArtistByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Artist?> FindArtistBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> ArtistSlugTakenAsync(string slug, CancellationToken cancellationToken);

    Task<Genre?> FindGenreAsync(string slug, CancellationToken cancellationToken);

    Task<TrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackRow>> ListArtistTracksAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Public, Ready, not deleted — newest first.</summary>
    Task<IReadOnlyList<TrackRow>> ListNewReleasesAsync(int limit, CancellationToken cancellationToken);

    void Add(Artist artist);

    void Add(Track track);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

internal static class TrackMapping
{
    public static TrackDto ToDto(this TrackRow row, bool isOwner) => new(
        row.Track.Id,
        row.Track.Title,
        row.Track.Description,
        row.Track.Status,
        row.Track.Visibility,
        row.Track.DurationMs,
        isOwner ? row.Track.ProcessingError : null,
        row.Track.CreatedAt,
        row.Track.PublishedAt,
        new ArtistRefDto(row.Artist.Id, row.Artist.Slug, row.Artist.Name),
        row.Genre is null ? null : new GenreDto(row.Genre.Slug, row.Genre.Name),
        isOwner,
        row.Track.Status == TrackStatus.Ready);

    public static ArtistDto ToDto(this Artist artist) => new(artist.Id, artist.Slug, artist.Name, artist.Bio, artist.CreatedAt);
}
