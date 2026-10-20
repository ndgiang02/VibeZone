using VibeZone.SharedKernel;

namespace VibeZone.Modules.Playlists.Domain;

public enum PlaylistVisibility
{
    Public,
    Private,
}

/// <summary>A track's place in a playlist (1-based, contiguous).</summary>
public sealed class PlaylistTrack
{
    private PlaylistTrack() { }

    internal PlaylistTrack(Guid playlistId, Guid trackId, int position, DateTimeOffset addedAt)
    {
        PlaylistId = playlistId;
        TrackId = trackId;
        Position = position;
        AddedAt = addedAt;
    }

    public Guid PlaylistId { get; private set; }

    public Guid TrackId { get; private set; }

    public int Position { get; internal set; }

    public DateTimeOffset AddedAt { get; private set; }
}

/// <summary>
/// A user's ordered list of tracks. Public playlists can be found and opened by anyone; private ones only by
/// their owner. A track appears at most once. Tracks the viewer can't see are hidden when rendering.
/// </summary>
public sealed class Playlist
{
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 1000;
    public const int MaxTracks = 500;

    private readonly List<PlaylistTrack> _tracks = [];

    private Playlist() { }

    public static Playlist Create(Guid ownerUserId, string title, string? description, PlaylistVisibility visibility, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        OwnerUserId = ownerUserId,
        Title = title,
        Description = NormalizeDescription(description),
        Visibility = visibility,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PlaylistVisibility Visibility { get; private set; }

    /// <summary>Denormalised for list views.</summary>
    public int TrackCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<PlaylistTrack> Tracks => _tracks;

    public bool IsVisibleTo(Guid? viewerUserId, bool viewerIsModerator) =>
        Visibility == PlaylistVisibility.Public || viewerUserId == OwnerUserId || viewerIsModerator;

    public static string NormalizeTitle(string? title) => string.Join(' ', (title ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static DomainError? Validate(string title, string? description, PlaylistVisibility visibility) =>
        title.Length is 0 or > MaxTitleLength
            ? DomainError.Validation("playlist_title_invalid", $"Title must be 1-{MaxTitleLength} characters.", "title")
            : description is { Length: > MaxDescriptionLength }
                ? DomainError.Validation("playlist_description_too_long", $"Description must be at most {MaxDescriptionLength} characters.", "description")
                : !Enum.IsDefined(visibility)
                    ? DomainError.Validation("playlist_visibility_invalid", "Unknown visibility.", "visibility")
                    : null;

    public void Update(string title, string? description, PlaylistVisibility visibility, DateTimeOffset now)
    {
        Title = title;
        Description = NormalizeDescription(description);
        Visibility = visibility;
        UpdatedAt = now;
    }

    /// <summary>Appends the track; adding one that's already there changes nothing.</summary>
    public DomainError? Add(Guid trackId, DateTimeOffset now)
    {
        if (_tracks.Any(t => t.TrackId == trackId))
        {
            return null;
        }

        if (_tracks.Count >= MaxTracks)
        {
            return DomainError.Validation("playlist_full", $"Playlists can hold at most {MaxTracks} tracks.");
        }

        _tracks.Add(new PlaylistTrack(Id, trackId, _tracks.Count + 1, now));
        Touch(now);
        return null;
    }

    public bool Remove(Guid trackId, DateTimeOffset now)
    {
        var item = _tracks.Find(t => t.TrackId == trackId);
        if (item is null)
        {
            return false;
        }

        _tracks.Remove(item);
        Renumber(_tracks.OrderBy(t => t.Position).Select(t => t.TrackId).ToList());
        Touch(now);
        return true;
    }

    /// <summary>
    /// Reorders the given tracks among the positions they occupy now; tracks left out (e.g. ones the owner
    /// can't see any more, such as another artist's track that became private) keep their places.
    /// </summary>
    public DomainError? Reorder(IReadOnlyList<Guid> trackIds, DateTimeOffset now)
    {
        if (trackIds.Count == 0 || trackIds.Distinct().Count() != trackIds.Count || trackIds.Any(id => _tracks.All(t => t.TrackId != id)))
        {
            return DomainError.Validation("playlist_order_mismatch", "List each track of the playlist at most once.", "trackIds");
        }

        var slots = _tracks.Where(t => trackIds.Contains(t.TrackId)).Select(t => t.Position).Order().ToList();
        for (var i = 0; i < trackIds.Count; i++)
        {
            _tracks.Single(t => t.TrackId == trackIds[i]).Position = slots[i];
        }

        Touch(now);
        return null;
    }

    private void Renumber(List<Guid> order)
    {
        for (var i = 0; i < order.Count; i++)
        {
            _tracks.Single(t => t.TrackId == order[i]).Position = i + 1;
        }
    }

    private void Touch(DateTimeOffset now)
    {
        TrackCount = _tracks.Count;
        UpdatedAt = now;
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
