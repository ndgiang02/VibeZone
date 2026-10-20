using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Playlists.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Playlists.Endpoints;

internal static class PlaylistEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var playlists = api.MapGroup("/playlists").WithTags("Playlists");
        playlists.MapGet("/", ListPublic).WithName("ListPublicPlaylists").Produces<ApiListResponse<PlaylistDto>>()
            .WithSummary("Public playlists with tracks: matching q (accent-insensitive title search), or newest first (paged).");
        playlists.MapPost("/", Create).WithName("CreatePlaylist").RequireAuthorization()
            .Produces<ApiResponse<PlaylistDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);
        playlists.MapGet("/{playlistId:guid}", Get).WithName("GetPlaylist")
            .Produces<ApiResponse<PlaylistDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Private playlists: owner only (404 otherwise). Tracks: GET /playlists/{id}/tracks.");
        playlists.MapPatch("/{playlistId:guid}", Update).WithName("UpdatePlaylist").RequireAuthorization()
            .Produces<ApiResponse<PlaylistDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. Send the full object.");
        playlists.MapDelete("/{playlistId:guid}", Delete).WithName("DeletePlaylist").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        playlists.MapPost("/{playlistId:guid}/tracks", AddTrack).WithName("AddPlaylistTrack").RequireAuthorization()
            .Produces<ApiResponse<PlaylistDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. Appends a track you can see; adding one that's already there is a no-op.");
        playlists.MapDelete("/{playlistId:guid}/tracks/{trackId:guid}", RemoveTrack).WithName("RemovePlaylistTrack").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        playlists.MapPut("/{playlistId:guid}/tracks/order", Reorder).WithName("ReorderPlaylist").RequireAuthorization()
            .Produces<ApiResponse<PlaylistDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Owner only. trackIds = the tracks to reorder (usually all the caller sees), in their new order; they swap among their current positions and tracks left out keep theirs.");

        api.MapGet("/me/playlists", ListMine).WithTags("Playlists").WithName("ListMyPlaylists").RequireAuthorization()
            .Produces<ApiListResponse<PlaylistDto>>()
            .WithSummary("The signed-in user's playlists, recently updated first (paged). containsTrack fills PlaylistDto.containsTrack.");
    }

    private static bool IsModerator(ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);

    private static async Task<IResult> ListPublic(string? q, int? page, int? pageSize, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await playlists.ListPublicAsync(q, new PageRequest(page, pageSize), user.UserId, ct));

    private static async Task<IResult> Create(SavePlaylistRequest request, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.CreateAsync(user.RequireUserId(), request, ct)).ToResult(p => ApiResults.Created($"/api/v1/playlists/{p.Id}", p));

    private static async Task<IResult> Get(Guid playlistId, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.GetAsync(playlistId, user.UserId, IsModerator(user), ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Update(Guid playlistId, SavePlaylistRequest request, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.UpdateAsync(user.RequireUserId(), playlistId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Delete(Guid playlistId, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.DeleteAsync(user.RequireUserId(), playlistId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> AddTrack(Guid playlistId, AddPlaylistTrackRequest request, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.AddTrackAsync(user.RequireUserId(), playlistId, request.TrackId, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> RemoveTrack(Guid playlistId, Guid trackId, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.RemoveTrackAsync(user.RequireUserId(), playlistId, trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> Reorder(Guid playlistId, ReorderPlaylistRequest request, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        (await playlists.ReorderAsync(user.RequireUserId(), playlistId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> ListMine(int? page, int? pageSize, Guid? containsTrack, PlaylistService playlists, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await playlists.ListMineAsync(user.RequireUserId(), new PageRequest(page, pageSize), containsTrack, ct));
}
