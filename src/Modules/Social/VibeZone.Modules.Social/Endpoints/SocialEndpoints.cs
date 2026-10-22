using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Social.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Endpoints;

internal static class SocialEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var tracks = api.MapGroup("/tracks/{trackId:guid}").WithTags("Social");
        tracks.MapGet("/engagement", GetEngagement).WithName("GetTrackEngagement")
            .Produces<ApiResponse<TrackEngagementDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Like and comment counts, and whether you liked it.");
        tracks.MapPut("/like", Like).WithName("LikeTrack").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<TrackEngagementDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Like a track you can play (idempotent).");
        tracks.MapDelete("/like", Unlike).WithName("UnlikeTrack").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<TrackEngagementDto>>();
        tracks.MapGet("/comments", ListComments).WithName("ListTrackComments")
            .Produces<ApiListResponse<CommentDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Top-level comments newest first (paged), each with its replies oldest first.");
        tracks.MapPost("/comments", PostComment).WithName("PostTrackComment").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<CommentDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Plain text, 1-1000 characters. timestampMs pins it to a moment of the track; parentId makes it a reply.");

        var comments = api.MapGroup("/comments/{commentId:guid}").WithTags("Social").RequireAuthorization();
        comments.MapPatch("/", EditComment).WithName("EditComment").RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<CommentDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Author only.");
        comments.MapDelete("/", DeleteComment).WithName("DeleteComment")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Author, the track's owner or a moderator.");

        var artists = api.MapGroup("/artists/{artistId:guid}").WithTags("Social");
        artists.MapGet("/followers/summary", GetFollowSummary).WithName("GetArtistFollowSummary")
            .Produces<ApiResponse<FollowSummaryDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        artists.MapPut("/follow", Follow).WithName("FollowArtist").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<FollowSummaryDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Idempotent. You can't follow your own artist profile.");
        artists.MapDelete("/follow", Unfollow).WithName("UnfollowArtist").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<FollowSummaryDto>>();

        var me = api.MapGroup("/me").WithTags("Library").RequireAuthorization();
        me.MapGet("/likes/ids", ListLikedIds).WithName("ListMyLikedTrackIds").Produces<ApiListResponse<Guid>>()
            .WithSummary("Ids of the tracks you liked (newest first, at most 5000), to mark hearts in lists.");
        me.MapGet("/following", ListFollowing).WithName("ListMyFollowedArtists").Produces<ApiListResponse<FollowedArtistDto>>()
            .WithSummary("Artists you follow, newest first (paged).");
    }

    private static async Task<IResult> GetEngagement(Guid trackId, LikeService likes, ICurrentUser user, CancellationToken ct) =>
        (await likes.GetEngagementAsync(trackId, user.UserId, user.IsModerator(), ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Like(Guid trackId, LikeService likes, ICurrentUser user, CancellationToken ct) =>
        (await likes.LikeAsync(user.RequireUserId(), trackId, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Unlike(Guid trackId, LikeService likes, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Ok(await likes.UnlikeAsync(user.RequireUserId(), trackId, ct));

    private static async Task<IResult> ListComments(Guid trackId, int? page, int? pageSize, CommentService comments, ICurrentUser user, CancellationToken ct) =>
        (await comments.ListAsync(trackId, user.UserId, user.IsModerator(), new PageRequest(page, pageSize), ct)).ToResult(ApiResults.Page);

    private static async Task<IResult> PostComment(Guid trackId, PostCommentRequest request, CommentService comments, ICurrentUser user, CancellationToken ct) =>
        (await comments.PostAsync(user.RequireUserId(), trackId, request, ct)).ToResult(c => ApiResults.Created($"/api/v1/tracks/{trackId}/comments", c));

    private static async Task<IResult> EditComment(Guid commentId, EditCommentRequest request, CommentService comments, ICurrentUser user, CancellationToken ct) =>
        (await comments.EditAsync(user.RequireUserId(), commentId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> DeleteComment(Guid commentId, CommentService comments, ICurrentUser user, CancellationToken ct) =>
        (await comments.DeleteAsync(user.RequireUserId(), user.IsModerator(), commentId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> GetFollowSummary(Guid artistId, FollowService follows, ICurrentUser user, CancellationToken ct) =>
        (await follows.GetSummaryAsync(artistId, user.UserId, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Follow(Guid artistId, FollowService follows, ICurrentUser user, CancellationToken ct) =>
        (await follows.FollowAsync(user.RequireUserId(), artistId, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Unfollow(Guid artistId, FollowService follows, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Ok(await follows.UnfollowAsync(user.RequireUserId(), artistId, ct));

    private static async Task<IResult> ListLikedIds(LikeService likes, ICurrentUser user, CancellationToken ct) =>
        ApiResults.List(await likes.ListLikedIdsAsync(user.RequireUserId(), ct));

    private static async Task<IResult> ListFollowing(int? page, int? pageSize, FollowService follows, ICurrentUser user, CancellationToken ct) =>
        ApiResults.Page(await follows.ListFollowingAsync(user.RequireUserId(), new PageRequest(page, pageSize), ct));
}
