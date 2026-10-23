using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Search;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Endpoints;

internal static class SearchEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var search = api.MapGroup("/search").WithTags("Search");
        search.MapGet("/", Overview).WithName("Search").Produces<ApiResponse<SearchResultsDto>>()
            .WithSummary("Top matches of each kind (6) with totals. Accent-insensitive, prefix and typo tolerant. Playlists: GET /playlists?q=.");
        search.MapGet("/tracks", Tracks).WithName("SearchTracks").Produces<ApiListResponse<TrackDto>>()
            .WithSummary("Ready public tracks matching q (title, description, artist name), best first (paged).");
        search.MapGet("/artists", Artists).WithName("SearchArtists").Produces<ApiListResponse<ArtistDto>>();
        search.MapGet("/albums", Albums).WithName("SearchAlbums").Produces<ApiListResponse<AlbumDto>>();
    }

    private static async Task<IResult> Overview(string? q, SearchService search, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Ok(await search.OverviewAsync(q, user.UserId, ct));

    private static async Task<IResult> Tracks(string? q, int? page, int? pageSize, SearchService search, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await search.TracksAsync(q, new PageRequest(page, pageSize), user.UserId, ct));

    private static async Task<IResult> Artists(string? q, int? page, int? pageSize, SearchService search, CancellationToken ct) =>
        ApiResults.Page(await search.ArtistsAsync(q, new PageRequest(page, pageSize), ct));

    private static async Task<IResult> Albums(string? q, int? page, int? pageSize, SearchService search, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await search.AlbumsAsync(q, new PageRequest(page, pageSize), user.UserId, ct));
}
