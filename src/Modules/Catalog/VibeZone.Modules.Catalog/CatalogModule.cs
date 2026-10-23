using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Modules.Catalog.Application.Genres;
using VibeZone.Modules.Catalog.Application.Search;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Endpoints;
using VibeZone.Modules.Catalog.Infrastructure;
using VibeZone.Modules.Identity.Contracts;

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
        services.AddScoped<IArtistCatalog, EfArtistCatalog>();
        services.AddScoped<IPublicProfileListener, EfArtistProfileSync>();
        services.AddScoped<ArtistService>();
        services.AddScoped<AlbumService>();
        services.AddScoped<SearchService>();
        services.AddScoped<ISearchQueries, EfSearchQueries>();
        services.AddScoped<TrackService>();
        services.AddScoped<TrackFeedService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        GenreEndpoints.Map(api);
        TrackEndpoints.Map(api);
        AlbumEndpoints.Map(api);
        SearchEndpoints.Map(api);
        FeedEndpoints.Map(api);
    }
}
