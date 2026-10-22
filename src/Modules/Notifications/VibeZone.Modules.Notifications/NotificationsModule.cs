using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Infrastructure.Outbox;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.Modules.Notifications.Application;
using VibeZone.Modules.Notifications.Endpoints;
using VibeZone.Modules.Notifications.Infrastructure;
using VibeZone.Modules.Social.Contracts;

namespace VibeZone.Modules.Notifications;

public sealed class NotificationsModule : IModule
{
    public const string Schema = "notifications";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // One instance in MVP: pushes reach connections on the instance that dispatched the event. Scaling out
        // needs a SignalR backplane (Redis) — see ADR-0012.
        services.AddSignalR();
        services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationPush, SignalRNotificationPush>();
        services.AddScoped<NotificationService>();
        services.AddIntegrationEventConsumer<TrackLiked, NotificationHandlers>();
        services.AddIntegrationEventConsumer<ArtistFollowed, NotificationHandlers>();
        services.AddIntegrationEventConsumer<CommentPosted, NotificationHandlers>();
        services.AddIntegrationEventConsumer<TrackProcessed, NotificationHandlers>();
        services.AddIntegrationEventConsumer<ReportResolved, NotificationHandlers>();
        services.AddIntegrationEventConsumer<ContentRemoved, NotificationHandlers>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => NotificationEndpoints.Map(api);
}
