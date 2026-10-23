using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Endpoints;

internal static class FeedEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapGet("/tracks/trending", ListTrending).WithTags("Catalog").WithName("ListTrendingTracks")
            .Produces<ApiListResponse<TrackDto>>()
            .WithSummary("Trending public tracks: valid listens and likes over the last 7 days, recent days weigh more (paged, top 100).");
        api.MapGet("/tracks/{trackId:guid}/related", ListRelated).WithTags("Catalog").WithName("ListRelatedTracks")
            .Produces<ApiListResponse<TrackDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Public tracks by the same artist or in the same genre (not personalised), at most 12.");

        var me = api.MapGroup("/me").WithTags("Library").RequireAuthorization();
        me.MapGet("/likes", ListLiked).WithName("ListMyLikedTracks").Produces<ApiListResponse<LikedTrackDto>>()
            .WithSummary("Tracks you liked, newest like first (paged). total counts likes of tracks you can no longer see too.");
        me.MapGet("/history", ListHistory).WithName("ListMyListeningHistory").Produces<ApiListResponse<HistoryEntryDto>>()
            .WithSummary("Recently played tracks, one entry per track, most recent first (paged).");

        api.MapGet("/admin/tracks", ListAll).WithTags("Admin").WithName("AdminListTracks")
            .RequireAuthorization(AuthorizationPolicies.Moderator)
            .Produces<ApiListResponse<TrackDto>>().ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Moderators: every track (any status/visibility, not deleted), newest first. q = title contains.");
    }

    private static async Task<IResult> ListTrending(int? page, int? pageSize, TrackFeedService feeds, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await feeds.ListTrendingAsync(new PageRequest(page, pageSize), user.UserId, ct));

    private static async Task<IResult> ListRelated(Guid trackId, TrackFeedService feeds, ICurrentUser user, CancellationToken ct) =>
        (await feeds.ListRelatedAsync(trackId, user.UserId, user.IsModerator(), ct)).ToResult(ApiResults.List);

    private static async Task<IResult> ListLiked(int? page, int? pageSize, TrackFeedService feeds, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await feeds.ListLikedAsync(user.RequireUserId(), new PageRequest(page, pageSize), ct));

    private static async Task<IResult> ListHistory(int? page, int? pageSize, TrackFeedService feeds, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await feeds.ListHistoryAsync(user.RequireUserId(), new PageRequest(page, pageSize), ct));

    private static async Task<IResult> ListAll(string? q, TrackStatus? status, bool? takenDown, int? page, int? pageSize, TrackFeedService feeds, CancellationToken ct) =>
        ApiResults.Page(await feeds.ListAllAsync(q, status, takenDown, new PageRequest(page, pageSize), ct));
}
