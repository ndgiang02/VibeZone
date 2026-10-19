using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Endpoints;

public sealed record TrackListResponse(IReadOnlyList<TrackDto> Items);

internal static class TrackEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var tracks = api.MapGroup("/tracks").WithTags("Catalog");
        tracks.MapGet("/", ListNewReleases).WithName("ListNewReleases").Produces<TrackListResponse>()
            .WithSummary("Newest public tracks that are ready to play.");
        tracks.MapGet("/{trackId:guid}", GetTrack).WithName("GetTrack").Produces<TrackDto>().ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapPost("/", CreateTrack).WithName("CreateTrack").RequireAuthorization()
            .Produces<TrackDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden);
        tracks.MapPatch("/{trackId:guid}", UpdateTrack).WithName("UpdateTrack").RequireAuthorization()
            .Produces<TrackDto>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        tracks.MapDelete("/{trackId:guid}", DeleteTrack).WithName("DeleteTrack").RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        var me = api.MapGroup("/me").WithTags("Catalog").RequireAuthorization();
        me.MapGet("/tracks", ListMyTracks).WithName("ListMyTracks").Produces<TrackListResponse>();
        me.MapGet("/artist", GetMyArtist).WithName("GetMyArtist").Produces<ArtistDto>().ProducesProblem(StatusCodes.Status404NotFound);
        me.MapPost("/artist", CreateMyArtist).WithName("CreateMyArtist")
            .Produces<ArtistDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Become an artist: creates the profile and grants the Artist role.");

        api.MapGet("/artists/{slug}", GetArtistProfile).WithTags("Catalog").WithName("GetArtistProfile")
            .Produces<ArtistProfileDto>().ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static bool IsModerator(ICurrentUser user) => user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Admin);

    private static async Task<IResult> ListNewReleases(TrackService tracks, ICurrentUser user, int? limit, CancellationToken ct) =>
        TypedResults.Ok(new TrackListResponse(await tracks.ListNewReleasesAsync(limit ?? 24, user.UserId, ct)));

    private static async Task<IResult> GetTrack(Guid trackId, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.GetAsync(trackId, user.UserId, IsModerator(user), ct)).ToResult(t => TypedResults.Ok(t));

    private static async Task<IResult> CreateTrack(SaveTrackRequest request, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.CreateAsync(user.RequireUserId(), request, ct)).ToResult(t => TypedResults.Created($"/api/v1/tracks/{t.Id}", t));

    private static async Task<IResult> UpdateTrack(Guid trackId, SaveTrackRequest request, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.UpdateAsync(user.RequireUserId(), trackId, request, ct)).ToResult(t => TypedResults.Ok(t));

    private static async Task<IResult> DeleteTrack(Guid trackId, TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        (await tracks.DeleteAsync(user.RequireUserId(), trackId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> ListMyTracks(TrackService tracks, ICurrentUser user, CancellationToken ct) =>
        TypedResults.Ok(new TrackListResponse(await tracks.ListMineAsync(user.RequireUserId(), ct)));

    private static async Task<IResult> GetMyArtist(ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.GetForUserAsync(user.RequireUserId(), ct)).ToResult(a => TypedResults.Ok(a));

    private static async Task<IResult> CreateMyArtist(CreateArtistRequest request, ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.CreateForUserAsync(user.RequireUserId(), request, ct)).ToResult(a => TypedResults.Created($"/api/v1/artists/{a.Slug}", a));

    private static async Task<IResult> GetArtistProfile(string slug, ArtistService artists, ICurrentUser user, CancellationToken ct) =>
        (await artists.GetProfileAsync(slug, user.UserId, IsModerator(user), ct)).ToResult(p => TypedResults.Ok(p));
}
