using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Analytics.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Analytics.Endpoints;

internal static class AnalyticsEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/plays", RecordPlays).WithTags("Analytics").WithName("RecordPlayEvents").RequireRateLimiting(RateLimitPolicies.Plays)
            .Produces<ApiResponse<PlayBatchResultDto>>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Player events (1-50 per batch; anonymous allowed for public tracks). Idempotent by event id. A valid listen is ≥ 30 s of playback (half of tracks under 60 s), once per listener and track per 30 minutes.");
        api.MapGet("/tracks/{trackId:guid}/stats", GetStats).WithTags("Analytics").WithName("GetTrackStats")
            .Produces<ApiResponse<TrackStatsDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("All-time plays (started) and valid listens.");
    }

    private static async Task<IResult> RecordPlays(PlayBatchRequest request, PlayService plays, ICurrentUser user, CancellationToken ct) =>
        (await plays.RecordAsync(user.UserId, request, ct)).ToResult(r => ApiResults.Accepted("/api/v1/plays", r));

    private static async Task<IResult> GetStats(Guid trackId, PlayService plays, ICurrentUser user, CancellationToken ct) =>
        (await plays.GetStatsAsync(trackId, user.UserId, user.IsModerator(), ct)).ToResult(ApiResults.Ok);
}
