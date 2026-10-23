using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Notifications.Application;
using VibeZone.Modules.Notifications.Infrastructure;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Notifications.Endpoints;

internal static class NotificationEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var me = api.MapGroup("/me/notifications").WithTags("Notifications").RequireAuthorization();
        me.MapGet("/", List).WithName("ListMyNotifications").Produces<ApiListResponse<NotificationDto>>()
            .WithSummary("Newest first (paged). unreadOnly=true for unread ones. Realtime: SignalR hub /api/v1/hubs/notifications sends 'unread' { count }.");
        me.MapGet("/unread-count", UnreadCount).WithName("GetMyUnreadNotificationCount").Produces<ApiResponse<UnreadCountDto>>();
        me.MapPost("/{notificationId:guid}/read", MarkRead).WithName("MarkNotificationRead")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        me.MapPost("/read-all", MarkAllRead).WithName("MarkAllNotificationsRead").Produces(StatusCodes.Status204NoContent);

        // Apps authenticate with ?access_token= here (WebSockets can't send headers); see BearerToken.
        // No CSRF token: browsers can't attach one to the WebSocket upgrade, and the hub is server → client only
        // (no hub methods change state), so a forged negotiate gains nothing — the response isn't readable cross-site.
        api.MapHub<NotificationsHub>(NotificationsHub.Path).RequireAuthorization().DisableCsrf();
    }

    private static async Task<IResult> List(bool? unreadOnly, int? page, int? pageSize, NotificationService notifications, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await notifications.ListAsync(user.RequireUserId(), unreadOnly ?? false, new PageRequest(page, pageSize), ct));

    private static async Task<IResult> UnreadCount(NotificationService notifications, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Ok(await notifications.CountUnreadAsync(user.RequireUserId(), ct));

    private static async Task<IResult> MarkRead(Guid notificationId, NotificationService notifications, ICurrentUser user, CancellationToken ct) =>
        (await notifications.MarkReadAsync(user.RequireUserId(), notificationId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> MarkAllRead(NotificationService notifications, ICurrentUser user, CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(user.RequireUserId(), ct);
        return TypedResults.NoContent();
    }
}
