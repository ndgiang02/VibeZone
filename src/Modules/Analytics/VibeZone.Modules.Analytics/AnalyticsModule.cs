using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Infrastructure.Outbox;
using VibeZone.Modules.Analytics.Application;
using VibeZone.Modules.Analytics.Contracts;
using VibeZone.Modules.Analytics.Endpoints;
using VibeZone.Modules.Analytics.Infrastructure;
using VibeZone.Modules.Social.Contracts;

namespace VibeZone.Modules.Analytics;

public sealed class AnalyticsModule : IModule
{
    public const string Schema = "analytics";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAnalyticsRepository, EfAnalyticsRepository>();
        services.AddScoped<PlayService>();
        services.AddScoped<IListeningHistory, EfListeningHistory>();
        services.AddScoped<ITrendingTracks, EfTrendingTracks>();
        services.AddIntegrationEventConsumer<TrackLiked, TrackLikedStatsHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => AnalyticsEndpoints.Map(api);
}
