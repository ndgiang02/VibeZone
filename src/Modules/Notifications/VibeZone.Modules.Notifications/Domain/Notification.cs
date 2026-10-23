using VibeZone.SharedKernel;

namespace VibeZone.Modules.Notifications.Domain;

public enum NotificationType
{
    TrackLiked,
    ArtistFollowed,
    CommentPosted,
    CommentReplied,
    TrackReady,
    TrackFailed,
    ReportResolved,
    ContentRemoved,
}

/// <summary>
/// Display snapshot taken when the notification is created (titles can change later; that's acceptable).
/// Never holds secrets, emails or tokens.
/// </summary>
public sealed class NotificationPayload
{
    /// <summary>Track, playlist or artist title/name.</summary>
    public string? Title { get; set; }

    /// <summary>Track to open (likes, comments, processing, takedowns).</summary>
    public Guid? TrackId { get; set; }

    public string? ArtistSlug { get; set; }

    /// <summary>Comment excerpt (plain text).</summary>
    public string? Excerpt { get; set; }

    /// <summary>Stable code: processing error, report outcome or reason.</summary>
    public string? Code { get; set; }
}

/// <summary>An in-app notification for one recipient. Created by event handlers, read and marked read by its recipient.</summary>
public sealed class Notification
{
    private Notification() { }

    public static Notification Create(
        Guid sourceEventId, Guid recipientUserId, NotificationType type, Guid? actorUserId, string subjectType, Guid subjectId, NotificationPayload payload, DateTimeOffset now) => new()
        {
            Id = EntityId.New(),
            SourceEventId = sourceEventId,
            RecipientUserId = recipientUserId,
            Type = type,
            ActorUserId = actorUserId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            Payload = payload,
            CreatedAt = now,
        };

    public Guid Id { get; private set; }

    /// <summary>The outbox message that caused it; unique per recipient (idempotent handlers).</summary>
    public Guid SourceEventId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public NotificationType Type { get; private set; }

    public Guid? ActorUserId { get; private set; }

    /// <summary><c>track</c>, <c>artist</c>, <c>comment</c>, <c>playlist</c> or <c>report</c>.</summary>
    public string SubjectType { get; private set; } = string.Empty;

    public Guid SubjectId { get; private set; }

    public NotificationPayload Payload { get; private set; } = new();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
