using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Domain;

/// <summary>A user likes a track (at most once).</summary>
public sealed class TrackLike
{
    private TrackLike() { }

    public TrackLike(Guid userId, Guid trackId, DateTimeOffset createdAt)
    {
        UserId = userId;
        TrackId = trackId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public Guid TrackId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>A user follows an artist profile (at most once; not their own).</summary>
public sealed class ArtistFollow
{
    private ArtistFollow() { }

    public ArtistFollow(Guid followerUserId, Guid artistId, DateTimeOffset createdAt)
    {
        FollowerUserId = followerUserId;
        ArtistId = artistId;
        CreatedAt = createdAt;
    }

    public Guid FollowerUserId { get; private set; }

    public Guid ArtistId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>
/// A plain-text comment on a track, optionally pinned to a moment of it (<see cref="TimestampMs"/>, "timed
/// comment"). Replies are one level deep: replying to a reply attaches to its top-level comment.
/// Deletion is soft; a deleted top-level comment hides its replies.
/// </summary>
public sealed class Comment
{
    public const int MaxBodyLength = 1000;
    public const int ExcerptLength = 120;

    private Comment() { }

    public static Comment Create(Guid trackId, Guid authorUserId, Comment? parent, string body, int? timestampMs, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        TrackId = trackId,
        AuthorUserId = authorUserId,
        ParentId = parent is null ? null : parent.ParentId ?? parent.Id,
        Body = body,
        // Replies inherit nothing: only top-level comments sit on the waveform.
        TimestampMs = parent is null ? timestampMs : null,
        CreatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid TrackId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    /// <summary>The top-level comment this replies to, or null.</summary>
    public Guid? ParentId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>Position in the track (ms) the comment refers to, or null.</summary>
    public int? TimestampMs { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>True when a moderator removed it (after a report), not its author.</summary>
    public bool RemovedByModeration { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    public string Excerpt => Body.Length <= ExcerptLength ? Body : Body[..ExcerptLength] + "…";

    /// <summary>Trims; keeps inner line breaks (rendered as text, never HTML).</summary>
    public static string NormalizeBody(string? body) => (body ?? string.Empty).Trim();

    public static DomainError? Validate(string body, int? timestampMs, int? trackDurationMs) =>
        body.Length is 0 or > MaxBodyLength
            ? DomainError.Validation("comment_body_invalid", $"Comment must be 1-{MaxBodyLength} characters.", "body")
            : timestampMs is < 0 || (timestampMs is { } t && trackDurationMs is { } d && t > d)
                ? DomainError.Validation("comment_timestamp_invalid", "Timestamp must be within the track.", "timestampMs")
                : null;

    public void Edit(string body, DateTimeOffset now)
    {
        Body = body;
        EditedAt = now;
    }

    public void Delete(DateTimeOffset now, bool byModeration)
    {
        DeletedAt ??= now;
        RemovedByModeration |= byModeration;
    }
}
