using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Domain;

public enum AlbumKind
{
    Album,
    Ep,
#pragma warning disable CA1720 // "Single" is the music term (a one-track release), not a type name
    Single,
#pragma warning restore CA1720
}

/// <summary>
/// A release grouping an artist's tracks in order. Visibility works like a track's: Unlisted albums are
/// reachable by link only, Private ones only by the owner. Its tracks keep their own visibility.
/// </summary>
public sealed class Album
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;
    public const int MaxTracks = 100;

    private Album() { }

    public static Album Create(Guid artistId, string title, AlbumKind kind, string? description, DateOnly? releaseDate, TrackVisibility visibility, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        ArtistId = artistId,
        Title = title,
        Kind = kind,
        Description = NormalizeDescription(description),
        ReleaseDate = releaseDate,
        Visibility = visibility,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid ArtistId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public AlbumKind Kind { get; private set; }

    public string? Description { get; private set; }

    public DateOnly? ReleaseDate { get; private set; }

    public TrackVisibility Visibility { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsVisibleTo(bool viewerOwnsIt, bool viewerIsModerator) =>
        DeletedAt is null && (viewerOwnsIt || viewerIsModerator || Visibility != TrackVisibility.Private);

    /// <summary>Shown in lists (artist page, search) — unlisted albums are link-only.</summary>
    public bool IsListedFor(bool viewerOwnsIt) => DeletedAt is null && (viewerOwnsIt || Visibility == TrackVisibility.Public);

    public static DomainError? Validate(string title, string? description) =>
        title.Length is 0 or > MaxTitleLength
            ? DomainError.Validation("album_title_invalid", $"Title must be 1-{MaxTitleLength} characters.", "title")
            : description is { Length: > MaxDescriptionLength }
                ? DomainError.Validation("album_description_too_long", $"Description must be at most {MaxDescriptionLength} characters.", "description")
                : null;

    public void Update(string title, AlbumKind kind, string? description, DateOnly? releaseDate, TrackVisibility visibility, DateTimeOffset now)
    {
        Title = title;
        Kind = kind;
        Description = NormalizeDescription(description);
        ReleaseDate = releaseDate;
        Visibility = visibility;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now) => DeletedAt ??= now;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
