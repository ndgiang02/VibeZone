using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Endpoints;

internal static class AlbumEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var albums = api.MapGroup("/albums").WithTags("Catalog");
        albums.MapPost("/", CreateAlbum).WithName("CreateAlbum").RequireAuthorization()
            .Produces<ApiResponse<AlbumDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden);
        albums.MapGet("/{albumId:guid}", GetAlbum).WithName("GetAlbum")
            .Produces<ApiResponse<AlbumDetailDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Album with the tracks the viewer can see, in order. Private albums: owner only (404 otherwise).");
        albums.MapPatch("/{albumId:guid}", UpdateAlbum).WithName("UpdateAlbum").RequireAuthorization()
            .Produces<ApiResponse<AlbumDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. Send the full object.");
        albums.MapDelete("/{albumId:guid}", DeleteAlbum).WithName("DeleteAlbum").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. The tracks stay; they just leave the album.");
        albums.MapPut("/{albumId:guid}/tracks", SetAlbumTracks).WithName("SetAlbumTracks").RequireAuthorization()
            .Produces<ApiResponse<AlbumDetailDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. Replaces the tracklist with these of your tracks, in this order (a track moves here from any other album).");

        api.MapGet("/me/albums", ListMyAlbums).WithTags("Catalog").WithName("ListMyAlbums").RequireAuthorization()
            .Produces<ApiListResponse<AlbumDto>>()
            .WithSummary("The signed-in artist's albums (any visibility), newest first.");
    }

    private static bool IsModerator(ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);

    private static async Task<IResult> CreateAlbum(SaveAlbumRequest request, AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        (await albums.CreateAsync(user.RequireUserId(), request, ct)).ToResult(a => ApiResults.Created($"/api/v1/albums/{a.Id}", a));

    private static async Task<IResult> GetAlbum(Guid albumId, AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        (await albums.GetAsync(albumId, user.UserId, IsModerator(user), ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> UpdateAlbum(Guid albumId, SaveAlbumRequest request, AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        (await albums.UpdateAsync(user.RequireUserId(), albumId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> DeleteAlbum(Guid albumId, AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        (await albums.DeleteAsync(user.RequireUserId(), albumId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> SetAlbumTracks(Guid albumId, SetAlbumTracksRequest request, AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        (await albums.SetTracksAsync(user.RequireUserId(), albumId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> ListMyAlbums(AlbumService albums, ICurrentUser user, CancellationToken ct) =>
        ApiResults.List(await albums.ListMineAsync(user.RequireUserId(), ct));
}
