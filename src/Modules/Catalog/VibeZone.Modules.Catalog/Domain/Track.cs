using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Domain;

public enum TrackStatus
{
    Draft,
    Processing,
    Ready,
    Failed,
    Cancelled,
}

public enum TrackVisibility
{
    Public,
    Unlisted,
    Private,
}

/// <summary>
/// A track and its processing lifecycle: Draft → Processing → Ready | Failed | Cancelled.
/// Failed/Cancelled tracks can be uploaded again (→ Processing). Ready tracks keep their audio.
/// </summary>
public sealed class Track
{
    public const int MaxTitleLength = 120;
    public const int MaxDescriptionLength = 2000;

    private Track() { }

    public static Track Create(Guid artistId, string title, string? description, int? genreId, TrackVisibility visibility, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        ArtistId = artistId,
        Title = title,
        Description = NormalizeDescription(description),
        GenreId = genreId,
        Visibility = visibility,
        Status = TrackStatus.Draft,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid ArtistId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int? GenreId { get; private set; }

    public TrackStatus Status { get; private set; }

    public TrackVisibility Visibility { get; private set; }

    public int? DurationMs { get; private set; }

    /// <summary>Stable error code from the last failed processing attempt (translated by the web app).</summary>
    public string? ProcessingError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>First time the track became Ready (feeds "new releases").</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Version of the current cover artwork (stored by the Media module), or null if none.</summary>
    public Guid? ArtworkId { get; private set; }

    /// <summary>The album this track belongs to (same artist), if any.</summary>
    public Guid? AlbumId { get; private set; }

    /// <summary>1-based position on <see cref="AlbumId"/>.</summary>
    public int? TrackNumber { get; private set; }

    public void SetAlbum(Guid? albumId, int? trackNumber, DateTimeOffset now)
    {
        AlbumId = albumId;
        TrackNumber = albumId is null ? null : trackNumber;
        UpdatedAt = now;
    }

    public bool CanStartProcessing => Status is TrackStatus.Draft or TrackStatus.Failed or TrackStatus.Cancelled;

    /// <summary>When moderation took the track down (null if it is up).</summary>
    public DateTimeOffset? TakenDownAt { get; private set; }

    /// <summary>Report reason code behind the takedown (copyright, abuse, spam, other).</summary>
    public string? TakedownReason { get; private set; }

    public bool IsTakenDown => TakenDownAt is not null;

    /// <summary>
    /// The single visibility rule (docs/architecture/data-model.md "Ownership and visibility"). A taken-down track
    /// stays visible to its owner (so Studio can show why) and to moderators, and is hidden from everyone else.
    /// </summary>
    public bool IsVisibleTo(bool viewerOwnsIt, bool viewerIsModerator) =>
        DeletedAt is null && (viewerOwnsIt || viewerIsModerator || (!IsTakenDown && Status == TrackStatus.Ready && Visibility != TrackVisibility.Private));

    /// <summary>Taken-down tracks play for moderators only (to review them), not for their owner.</summary>
    public bool IsPlayableBy(bool viewerOwnsIt, bool viewerIsModerator) =>
        Status == TrackStatus.Ready && IsVisibleTo(viewerOwnsIt, viewerIsModerator) && (!IsTakenDown || viewerIsModerator);

    /// <summary>Listed in feeds, search, trending, related and artist pages (unlisted tracks are link-only).</summary>
    public bool IsListed => DeletedAt is null && !IsTakenDown && Status == TrackStatus.Ready && Visibility == TrackVisibility.Public;

    public void TakeDown(string reasonCode, DateTimeOffset now)
    {
        TakenDownAt ??= now;
        TakedownReason = reasonCode;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        TakenDownAt = null;
        TakedownReason = null;
        UpdatedAt = now;
    }

    public static string NormalizeTitle(string? title) => string.Join(' ', (title ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static DomainError? Validate(string title, string? description) =>
        title.Length is 0 or > MaxTitleLength
            ? DomainError.Validation("track_title_invalid", $"Title must be 1-{MaxTitleLength} characters.", "title")
            : description is { Length: > MaxDescriptionLength }
                ? DomainError.Validation("track_description_too_long", $"Description must be at most {MaxDescriptionLength} characters.", "description")
                : null;

    public void UpdateMetadata(string title, string? description, int? genreId, TrackVisibility visibility, DateTimeOffset now)
    {
        Title = title;
        Description = NormalizeDescription(description);
        GenreId = genreId;
        Visibility = visibility;
        UpdatedAt = now;
    }

    public void SetArtwork(Guid? artworkId, DateTimeOffset now)
    {
        ArtworkId = artworkId;
        UpdatedAt = now;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        if (!CanStartProcessing)
        {
            throw new InvalidOperationException($"Track in status {Status} cannot start processing.");
        }

        Status = TrackStatus.Processing;
        ProcessingError = null;
        UpdatedAt = now;
    }

    public void MarkReady(int durationMs, DateTimeOffset now)
    {
        EnsureProcessing();
        Status = TrackStatus.Ready;
        DurationMs = durationMs;
        PublishedAt ??= now;
        UpdatedAt = now;
    }

    public void MarkFailed(string reasonCode, DateTimeOffset now)
    {
        EnsureProcessing();
        Status = TrackStatus.Failed;
        ProcessingError = reasonCode;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureProcessing();
        Status = TrackStatus.Cancelled;
        UpdatedAt = now;
    }

    public void Delete(DateTimeOffset now) => DeletedAt ??= now;

    private void EnsureProcessing()
    {
        if (Status != TrackStatus.Processing)
        {
            throw new InvalidOperationException($"Track is {Status}, not Processing.");
        }
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
