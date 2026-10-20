using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

/// <param name="AvatarUrl">Small profile picture (≈160 px), or null.</param>
public sealed record ArtistRefDto(Guid Id, string Slug, string Name, string? AvatarUrl);

/// <param name="Avatar">Square profile picture: small ≈ 160 px, large ≈ 640 px.</param>
/// <param name="Banner">Wide 3:1 page cover: small ≈ 800 px, large ≈ 1600 px wide.</param>
public sealed record ArtistDto(Guid Id, string Slug, string Name, string? Bio, DateTimeOffset CreatedAt, ArtworkDto? Avatar, ArtworkDto? Banner);

/// <summary>
/// An image in two JPEG sizes (track cover: square 320/1200 px; artist avatar: square 160/640 px; artist
/// banner: 3:1, 800/1600 px wide). Relative URLs that change when the image changes.
/// </summary>
public sealed record ArtworkDto(string Small, string Large);

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
    bool Playable,
    ArtworkDto? Artwork,
    AlbumRefDto? Album,
    int? TrackNumber);

public sealed record AlbumRefDto(Guid Id, string Title);

/// <param name="TrackCount">Tracks the viewer can see.</param>
/// <param name="Cover">The cover of its first track that has one (albums have no separate cover upload).</param>
public sealed record AlbumDto(
    Guid Id,
    string Title,
    AlbumKind Kind,
    string? Description,
    DateOnly? ReleaseDate,
    TrackVisibility Visibility,
    DateTimeOffset CreatedAt,
    ArtistRefDto Artist,
    int TrackCount,
    int DurationMs,
    ArtworkDto? Cover,
    bool IsOwner);

/// <param name="Tracks">In album order; only tracks the viewer can see.</param>
public sealed record AlbumDetailDto(AlbumDto Album, IReadOnlyList<TrackDto> Tracks);

public sealed record SaveAlbumRequest(string? Title, AlbumKind Kind, string? Description, DateOnly? ReleaseDate, TrackVisibility Visibility);

/// <param name="TrackIds">The album's complete tracklist in order (the artist's own tracks; others are removed from it).</param>
public sealed record SetAlbumTracksRequest(IReadOnlyList<Guid>? TrackIds);

/// <param name="Albums">Albums listed for the viewer (public ones; all of them for the owner), newest first.</param>
public sealed record ArtistProfileDto(ArtistDto Artist, IReadOnlyList<TrackDto> Tracks, IReadOnlyList<AlbumDto> Albums);

public sealed record CreateArtistRequest(string? Slug, string? Name, string? Bio);

public sealed record SaveTrackRequest(string? Title, string? Description, string? GenreSlug, TrackVisibility Visibility);

/// <summary>A track with the rows needed to render and authorize it.</summary>
public sealed record TrackRow(Track Track, Artist Artist, Genre? Genre, Album? Album);

/// <summary>An album with its artist.</summary>
public sealed record AlbumRow(Album Album, Artist Artist);

/// <summary>Persistence port for the Catalog module.</summary>
public interface ICatalogRepository
{
    Task<Artist?> FindArtistByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Artist?> FindArtistBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<bool> ArtistSlugTakenAsync(string slug, CancellationToken cancellationToken);

    Task<Genre?> FindGenreAsync(string slug, CancellationToken cancellationToken);

    Task<TrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrackRow>> ListArtistTracksAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Not deleted, newest first.</summary>
    Task<Paged<TrackRow>> PageArtistTracksAsync(Guid artistId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Public, Ready, not deleted — newest first.</summary>
    Task<Paged<TrackRow>> PageNewReleasesAsync(PageRequest page, CancellationToken cancellationToken);

    Task<AlbumRow?> FindAlbumAsync(Guid albumId, CancellationToken cancellationToken);

    /// <summary>Not deleted, newest release first.</summary>
    Task<IReadOnlyList<Album>> ListArtistAlbumsAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Not-deleted tracks on the given albums, by album then track number.</summary>
    Task<IReadOnlyList<TrackRow>> ListAlbumTracksAsync(IReadOnlyCollection<Guid> albumIds, CancellationToken cancellationToken);

    /// <summary>The artist's not-deleted tracks, tracked for update.</summary>
    Task<IReadOnlyList<Track>> ListArtistTrackEntitiesAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Rows for the ids (any status); the caller filters by visibility and orders.</summary>
    Task<IReadOnlyList<TrackRow>> ListTracksAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Artist>> ListArtistsAsync(IReadOnlyCollection<Guid> artistIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<AlbumRow>> ListAlbumRowsAsync(IReadOnlyCollection<Guid> albumIds, CancellationToken cancellationToken);

    void Add(Artist artist);

    void Add(Track track);

    void Add(Album album);

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
        row.Artist.ToRef(),
        row.Genre is null ? null : new GenreDto(row.Genre.Slug, row.Genre.Name),
        isOwner,
        row.Track.Status == TrackStatus.Ready,
        row.Track.ToArtwork(),
        row.Album is { DeletedAt: null } album && album.IsVisibleTo(isOwner, false) ? new AlbumRefDto(album.Id, album.Title) : null,
        row.Album is { DeletedAt: null } ? row.Track.TrackNumber : null);

    public static ArtworkDto? ToArtwork(this Track track) =>
        track.ArtworkId is { } artworkId
            ? new ArtworkDto(
                TrackArtworkPaths.For(track.Id, artworkId, TrackArtworkPaths.Small),
                TrackArtworkPaths.For(track.Id, artworkId, TrackArtworkPaths.Large))
            : null;

    public static ArtistRefDto ToRef(this Artist artist) =>
        new(artist.Id, artist.Slug, artist.Name, artist.ToImages(ArtistImageSlot.Avatar)?.Small);

    /// <summary><paramref name="tracks"/>: the album's tracks the viewer can see, in order.</summary>
    public static AlbumDto ToDto(this Album album, Artist artist, IReadOnlyList<TrackRow> tracks, bool isOwner) => new(
        album.Id,
        album.Title,
        album.Kind,
        album.Description,
        album.ReleaseDate,
        album.Visibility,
        album.CreatedAt,
        artist.ToRef(),
        tracks.Count,
        tracks.Sum(t => t.Track.DurationMs ?? 0),
        tracks.Select(t => t.Track.ToArtwork()).FirstOrDefault(a => a is not null),
        isOwner);

    public static ArtistDto ToDto(this Artist artist) =>
        new(artist.Id, artist.Slug, artist.Name, artist.Bio, artist.CreatedAt, artist.ToImages(ArtistImageSlot.Avatar), artist.ToImages(ArtistImageSlot.Banner));

    private static ArtworkDto? ToImages(this Artist artist, ArtistImageSlot slot) =>
        (slot == ArtistImageSlot.Avatar ? artist.AvatarId : artist.BannerId) is { } imageId
            ? new ArtworkDto(ArtistImagePaths.For(artist.Id, slot, imageId, "small"), ArtistImagePaths.For(artist.Id, slot, imageId, "large"))
            : null;
}
