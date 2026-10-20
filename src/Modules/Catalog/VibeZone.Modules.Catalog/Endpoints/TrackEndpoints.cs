using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Endpoints;

internal static class TrackEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var tracks = api.MapGroup("/tracks").WithTags("Catalog");
        tracks.MapGet("/", ListNewReleases).WithName("ListNewReleases").Produces<ApiListResponse<TrackDto>>()
            .WithSummary("Newest public tracks that are ready to play (paged: page, pageSize ≤ 50).");
        tracks.MapGet("/{trackId:guid}", GetTrack).WithName("GetTrack").Produces<ApiResponse<TrackDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapPost("/", CreateTrack).WithName("CreateTrack").RequireAuthorization()
            .Produces<ApiResponse<TrackDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden);
        tracks.MapPatch("/{trackId:guid}", UpdateTrack).WithName("UpdateTrack").RequireAuthorization()
            .Produces<ApiResponse<TrackDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapDelete("/{trackId:guid}", DeleteTrack).WithName("DeleteTrack").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        var me = api.MapGroup("/me").WithTags("Catalog").RequireAuthorization();
        me.MapGet("/tracks", ListMyTracks).WithName("ListMyTracks").Produces<ApiListResponse<TrackDto>>()
            .WithSummary("The signed-in artist's tracks, newest first (paged: page, pageSize ≤ 50).");
        me.MapGet("/artist", GetMyArtist).WithName("GetMyArtist").Produces<ApiResponse<ArtistDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        me.MapPost("/artist", CreateMyArtist).WithName("CreateMyArtist")
            .Produces<ApiResponse<ArtistDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Become an artist: creates the profile and grants the Artist role.");

        // Composition: Playlists owns the order, Catalog owns the track DTOs (IPlaylistReader).
        api.MapGet("/playlists/{playlistId:guid}/tracks", ListPlaylistTracks).WithTags("Playlists").WithName("ListPlaylistTracks")
            .Produces<ApiListResponse<TrackDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("The playlist's tracks in order, without those the viewer can't see.");

        api.MapGet("/artists/{slug}", GetArtistProfile).WithTags("Catalog").WithName("GetArtistProfile")
            .Produces<ApiResponse<ArtistProfileDto>>().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static bool IsModerator(ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);

    private static async Task<IResult> ListNewReleases(TrackService tracks, ICurrentUser user, int? page, int? pageSize, CancellationToken ct) =>
        ApiResults.Page(await tracks.ListNewReleasesAsync(new PageRequest(page, pageSize), user.UserId, ct));

    private static async Task<IResult> GetTrack(Guid trackId, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.GetAsync(trackId, user.UserId, IsModerator(user), ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> CreateTrack(SaveTrackRequest request, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.CreateAsync(user.RequireUserId(), request, ct)).ToResult(t => ApiResults.Created($"/api/v1/tracks/{t.Id}", t));

    private static async Task<IResult> UpdateTrack(Guid trackId, SaveTrackRequest request, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.UpdateAsync(user.RequireUserId(), trackId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> DeleteTrack(Guid trackId, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.DeleteAsync(user.RequireUserId(), trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> ListMyTracks(TrackService tracks, ICurrentUser user, int? page, int? pageSize, CancellationToken ct) =>
        ApiResults.Page(await tracks.ListMineAsync(user.RequireUserId(), new PageRequest(page, pageSize), ct));

    private static async Task<IResult> GetMyArtist(ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.GetForUserAsync(user.RequireUserId(), ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> CreateMyArtist(CreateArtistRequest request, ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.CreateForUserAsync(user.RequireUserId(), request, ct)).ToResult(a => ApiResults.Created($"/api/v1/artists/{a.Slug}", a));

    private static async Task<IResult> ListPlaylistTracks(Guid playlistId, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.ListPlaylistTracksAsync(playlistId, user.UserId, IsModerator(user), ct)).ToResult(ApiResults.List);

    private static async Task<IResult> GetArtistProfile(string slug, ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.GetProfileAsync(slug, user.UserId, IsModerator(user), ct)).ToResult(ApiResults.Ok);
}
