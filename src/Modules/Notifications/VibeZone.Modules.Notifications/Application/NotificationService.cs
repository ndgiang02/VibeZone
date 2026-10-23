using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Notifications.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Notifications.Application;

public sealed record NotificationActorDto(Guid Id, string DisplayName, string Handle, string? AvatarUrl);

/// <param name="Title">Snapshot of the subject's title when it happened.</param>
/// <param name="Code">Processing error code, report outcome (actioned/rejected) or takedown reason.</param>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    NotificationActorDto? Actor,
    string SubjectType,
    Guid SubjectId,
    string? Title,
    Guid? TrackId,
    string? ArtistSlug,
    string? Excerpt,
    string? Code);

public sealed record UnreadCountDto(int Count);

/// <summary>Persistence port for the Notifications module.</summary>
public interface INotificationRepository
{
    /// <summary>Newest first.</summary>
    Task<Paged<Notification>> PageAsync(Guid recipientUserId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Tracked for update.</summary>
    Task<Notification?> FindAsync(Guid notificationId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(Guid recipientUserId, DateTimeOffset now, CancellationToken cancellationToken);

    void Add(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Realtime signal to the recipient's open apps (best-effort; clients also refetch on focus).</summary>
public interface INotificationPush
{
    Task PushUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken);
}

public sealed class NotificationService(INotificationRepository repository, IUserDirectory users, TimeProvider time)
{
    public async Task<Paged<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken)
    {
        var notifications = await repository.PageAsync(userId, unreadOnly, page, cancellationToken);
        var actorIds = notifications.Items.Where(n => n.ActorUserId is not null).Select(n => n.ActorUserId!.Value).Distinct().ToList();
        var actors = await users.GetAsync(actorIds, cancellationToken);
        return notifications.Map(n => new NotificationDto(
            n.Id,
            n.Type,
            n.CreatedAt,
            n.ReadAt,
            n.ActorUserId is { } a && actors.TryGetValue(a, out var actor) ? new NotificationActorDto(actor.Id, actor.DisplayName, actor.Handle, actor.AvatarUrl) : null,
            n.SubjectType,
            n.SubjectId,
            n.Payload.Title,
            n.Payload.TrackId,
            n.Payload.ArtistSlug,
            n.Payload.Excerpt,
            n.Payload.Code));
    }

    public async Task<UnreadCountDto> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await repository.CountUnreadAsync(userId, cancellationToken));

    /// <summary>Someone else's notification is reported as not found.</summary>
    public async Task<Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await repository.FindAsync(notificationId, cancellationToken);
        if (notification is null || notification.RecipientUserId != userId)
        {
            return Result.Failure(DomainError.NotFound("notification_not_found", "Notification not found."));
        }

        notification.MarkRead(time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.MarkAllReadAsync(userId, time.GetUtcNow(), cancellationToken);
}
