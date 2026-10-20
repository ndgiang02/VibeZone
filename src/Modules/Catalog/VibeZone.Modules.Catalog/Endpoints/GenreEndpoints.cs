using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Genres;

namespace VibeZone.Modules.Catalog.Endpoints;

internal static class GenreEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var genres = api.MapGroup("/genres").WithTags("Catalog");

        genres.MapGet("/", ListGenres)
            .WithName("ListGenres")
            .WithSummary("List all genres, ordered by name.");
    }

    private static async Task<Ok<ApiListResponse<GenreDto>>> ListGenres(IGenreQueries queries, HttpContext http, CancellationToken cancellationToken)
    {
        var items = await queries.ListAsync(cancellationToken);
        http.Response.Headers.CacheControl = "public, max-age=300";
        return ApiResults.List(items);
    }
}
