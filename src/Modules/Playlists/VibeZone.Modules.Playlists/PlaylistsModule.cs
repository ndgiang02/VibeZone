using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Playlists.Application;
using VibeZone.Modules.Playlists.Contracts;
using VibeZone.Modules.Playlists.Endpoints;
using VibeZone.Modules.Playlists.Infrastructure;

namespace VibeZone.Modules.Playlists;

public sealed class PlaylistsModule : IModule
{
    public const string Schema = "playlists";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPlaylistRepository, EfPlaylistRepository>();
        services.AddScoped<PlaylistService>();
        services.AddScoped<IPlaylistReader, PlaylistReader>();
        services.AddScoped<IPlaylistModeration, EfPlaylistModeration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api) => PlaylistEndpoints.Map(api);
}
