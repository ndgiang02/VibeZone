using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Infrastructure.Security;
using VibeZone.Modules.Notifications.Application;
using VibeZone.Modules.Notifications.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Notifications.Infrastructure;

// recipient_user_id / actor_user_id reference identity.users; those FKs are added in SQL by the migration
// because the Identity model belongs to another module (ADR-0001).

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications", NotificationsModule.Schema, t =>
            t.HasCheckConstraint("ck_notifications_type", $"type IN ({string.Join(", ", Enum.GetNames<NotificationType>().Select(n => $"'{ToSnake(n)}'"))})"));
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).ValueGeneratedNever();
        b.Property(n => n.Type).HasConversion(v => ToSnake(v.ToString()), v => Enum.Parse<NotificationType>(v.Replace("_", string.Empty), true)).HasMaxLength(32);
        b.Property(n => n.SubjectType).HasMaxLength(16).IsRequired();
        b.OwnsOne(n => n.Payload, p =>
        {
            p.ToJson("payload");
            p.Property(x => x.Title).HasMaxLength(200);
            p.Property(x => x.Excerpt).HasMaxLength(200);
            p.Property(x => x.Code).HasMaxLength(64);
        });
        b.HasIndex(n => new { n.RecipientUserId, n.CreatedAt }).IsDescending(false, true);
        b.HasIndex(n => n.RecipientUserId).HasDatabaseName("ix_notifications_unread").HasFilter("read_at IS NULL");
        b.HasIndex(n => new { n.SourceEventId, n.RecipientUserId }).IsUnique();
    }

    /// <summary>TrackLiked → track_liked (readable values, like the other lower-case enums).</summary>
    private static string ToSnake(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

internal sealed class EfNotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<Paged<Notification>> PageAsync(Guid recipientUserId, bool unreadOnly, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Set<Notification>().AsNoTracking().Where(n => n.RecipientUserId == recipientUserId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<Notification> items = total <= page.Skip
            ? []
            : await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<Notification>(items, page.Page, page.PageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken) =>
        db.Set<Notification>().CountAsync(n => n.RecipientUserId == recipientUserId && n.ReadAt == null, cancellationToken);

    public Task<Notification?> FindAsync(Guid notificationId, CancellationToken cancellationToken) =>
        db.Set<Notification>().SingleOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

    public Task MarkAllReadAsync(Guid recipientUserId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Set<Notification>().Where(n => n.RecipientUserId == recipientUserId && n.ReadAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);

    public void Add(Notification notification) => db.Set<Notification>().Add(notification);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Server → client only: clients receive <c>unread</c> (<c>{ count }</c>) and refetch. Users are addressed by the
/// <c>sub</c> claim. The hub is reachable under /api/v1/hubs/notifications so it shares the API's proxy routes.
/// </summary>
[Authorize]
internal sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string UnreadMethod = "unread";
}

internal sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirstValue(VibeZoneClaims.UserId);
}

internal sealed class SignalRNotificationPush(IHubContext<NotificationsHub> hub, INotificationRepository repository) : INotificationPush
{
    public async Task PushUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var count = await repository.CountUnreadAsync(recipientUserId, cancellationToken);
        await hub.Clients.User(recipientUserId.ToString()).SendAsync(NotificationsHub.UnreadMethod, new UnreadCountDto(count), cancellationToken);
    }
}
