using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using VibeZone.Modules.Catalog.Application.Genres;

namespace VibeZone.Modules.Catalog.Endpoints;

public sealed record GenreListResponse(IReadOnlyList<GenreDto> Items);

internal static class GenreEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var genres = api.MapGroup("/genres").WithTags("Catalog");

        genres.MapGet("/", ListGenres)
            .WithName("ListGenres")
            .WithSummary("List all genres, ordered by name.");
    }

    private static async Task<Ok<GenreListResponse>> ListGenres(IGenreQueries queries, HttpContext http, CancellationToken cancellationToken)
    {
        var items = await queries.ListAsync(cancellationToken);
        http.Response.Headers.CacheControl = "public, max-age=300";
        return TypedResults.Ok(new GenreListResponse(items));
    }
}
