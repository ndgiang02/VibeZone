using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Endpoints;
using VibeZone.Modules.Catalog.Infrastructure;

namespace VibeZone.Modules.Catalog;

public sealed class CatalogModule : IModule
{
    public const string Schema = "catalog";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGenreQueries, EfGenreQueries>();
        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<ITrackCatalog, EfTrackCatalog>();
        services.AddScoped<ArtistService>();
        services.AddScoped<TrackService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        GenreEndpoints.Map(api);
        TrackEndpoints.Map(api);
    }
}
