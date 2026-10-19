using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Identity.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Endpoints;

public sealed record SessionListResponse(IReadOnlyList<SessionDto> Items);

public sealed record RevokedSessionsResponse(int Revoked);

internal static class MeEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var me = api.MapGroup("/me").WithTags("Account").RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapGet("/", GetMe).WithName("GetMe").Produces<MeDto>();
        me.MapPatch("/profile", UpdateProfile).WithName("UpdateProfile").Produces<MeDto>().ProducesProblem(StatusCodes.Status400BadRequest);
        me.MapPost("/password", ChangePassword).WithName("ChangePassword").RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Change password; signs out every other session.");
        me.MapGet("/sessions", ListSessions).WithName("ListSessions").Produces<SessionListResponse>();
        me.MapDelete("/sessions/{sessionId:guid}", RevokeSession).WithName("RevokeSession")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        me.MapPost("/sessions/revoke-others", RevokeOtherSessions).WithName("RevokeOtherSessions").Produces<RevokedSessionsResponse>();
    }

    private static async Task<IResult> GetMe(ProfileService profiles, ICurrentUser user, CancellationToken ct) =>
        (await profiles.GetMeAsync(user.RequireUserId(), ct)).ToResult(me => TypedResults.Ok(me));

    private static async Task<IResult> UpdateProfile(UpdateProfileRequest request, ProfileService profiles, ICurrentUser user, CancellationToken ct) =>
        (await profiles.UpdateProfileAsync(user.RequireUserId(), request, ct)).ToResult(me => TypedResults.Ok(me));

    private static async Task<IResult> ChangePassword(ChangePasswordRequest request, AccountService accounts, ICurrentUser user, CancellationToken ct) =>
        (await accounts.ChangePasswordAsync(user.RequireUserId(), RequireSession(user), request, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> ListSessions(SessionService sessions, ICurrentUser user, CancellationToken ct) =>
        TypedResults.Ok(new SessionListResponse(await sessions.ListAsync(user.RequireUserId(), RequireSession(user), ct)));

    private static async Task<IResult> RevokeSession(Guid sessionId, SessionService sessions, ICurrentUser user, CancellationToken ct) =>
        (await sessions.RevokeAsync(user.RequireUserId(), sessionId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> RevokeOtherSessions(SessionService sessions, ICurrentUser user, CancellationToken ct) =>
        TypedResults.Ok(new RevokedSessionsResponse(await sessions.RevokeOthersAsync(user.RequireUserId(), RequireSession(user), ct)));

    private static Guid RequireSession(ICurrentUser user) =>
        user.SessionId ?? throw new InvalidOperationException("Authenticated request without a session id.");
}
