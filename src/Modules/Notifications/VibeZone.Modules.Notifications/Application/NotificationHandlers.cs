using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.Modules.Notifications.Domain;
using VibeZone.Modules.Social.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Notifications.Application;

/// <summary>
/// Turns other modules' events into notifications (delivered through the outbox, ADR-0004). Nobody is
/// notified about their own action. After the commit, recipients get a realtime unread-count signal.
/// </summary>
public sealed class NotificationHandlers(INotificationRepository repository, INotificationPush push, TimeProvider time) :
    IIntegrationEventConsumer<TrackLiked>,
    IIntegrationEventConsumer<ArtistFollowed>,
    IIntegrationEventConsumer<CommentPosted>,
    IIntegrationEventConsumer<TrackProcessed>,
    IIntegrationEventConsumer<ReportResolved>,
    IIntegrationEventConsumer<ContentRemoved>
{
    public Task HandleAsync(TrackLiked e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        if (e.UserId != e.TrackOwnerUserId)
        {
            Notify(context, e.TrackOwnerUserId, NotificationType.TrackLiked, e.UserId, "track", e.TrackId, new() { Title = e.TrackTitle, TrackId = e.TrackId });
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(ArtistFollowed e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        if (e.FollowerUserId != e.ArtistOwnerUserId)
        {
            Notify(context, e.ArtistOwnerUserId, NotificationType.ArtistFollowed, e.FollowerUserId, "artist", e.ArtistId, new() { ArtistSlug = e.ArtistSlug });
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(CommentPosted e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        // A new instance per notification: the payload is an owned (JSON) entity and can't be shared between rows.
        NotificationPayload Payload() => new() { Title = e.TrackTitle, TrackId = e.TrackId, Excerpt = e.Excerpt };
        // A reply notifies the replied-to author; the track owner hears about it once (as a reply if it's theirs).
        if (e.ParentAuthorUserId is { } parentAuthor && parentAuthor != e.AuthorUserId)
        {
            Notify(context, parentAuthor, NotificationType.CommentReplied, e.AuthorUserId, "comment", e.CommentId, Payload());
        }

        if (e.TrackOwnerUserId != e.AuthorUserId && e.TrackOwnerUserId != e.ParentAuthorUserId)
        {
            Notify(context, e.TrackOwnerUserId, NotificationType.CommentPosted, e.AuthorUserId, "comment", e.CommentId, Payload());
        }

        return Task.CompletedTask;
    }

    public Task HandleAsync(TrackProcessed e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        Notify(context, e.OwnerUserId, e.Succeeded ? NotificationType.TrackReady : NotificationType.TrackFailed, null, "track", e.TrackId,
            new() { Title = e.Title, TrackId = e.TrackId, Code = e.ErrorCode });
        return Task.CompletedTask;
    }

    public Task HandleAsync(ReportResolved e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        Notify(context, e.ReporterUserId, NotificationType.ReportResolved, null, "report", e.ReportId,
            new() { Title = e.SubjectTitle, Code = e.Outcome, TrackId = e.SubjectType == "track" ? e.SubjectId : null });
        return Task.CompletedTask;
    }

    public Task HandleAsync(ContentRemoved e, IntegrationEventContext context, CancellationToken cancellationToken)
    {
        Notify(context, e.OwnerUserId, NotificationType.ContentRemoved, null, e.SubjectType, e.SubjectId,
            new() { Title = e.SubjectTitle, Code = e.Reason, TrackId = e.SubjectType == "track" ? e.SubjectId : null });
        return Task.CompletedTask;
    }

    private void Notify(IntegrationEventContext context, Guid recipient, NotificationType type, Guid? actor, string subjectType, Guid subjectId, NotificationPayload payload)
    {
        repository.Add(Notification.Create(context.MessageId, recipient, type, actor, subjectType, subjectId, payload, time.GetUtcNow()));
        context.AfterCommit(ct => push.PushUnreadCountAsync(recipient, ct));
    }
}
