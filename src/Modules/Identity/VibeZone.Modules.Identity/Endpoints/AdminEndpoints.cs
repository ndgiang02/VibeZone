using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Identity.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Endpoints;

internal static class AdminEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var admin = api.MapGroup("/admin/users").WithTags("Admin").RequireAuthorization(AuthorizationPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);
        admin.MapGet("/", List).WithName("AdminListUsers").Produces<ApiListResponse<AdminUserDto>>()
            .WithSummary("Admins. Newest first (paged). q matches handle, display name or email; role filters by role.");
        admin.MapPut("/{userId:guid}/roles/{role}", Grant).WithName("AdminGrantRole")
            .Produces<ApiResponse<AdminUserDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Admins. Grant Moderator or Admin (idempotent, audited).");
        admin.MapDelete("/{userId:guid}/roles/{role}", Revoke).WithName("AdminRevokeRole")
            .Produces<ApiResponse<AdminUserDto>>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Admins. Revoke Moderator or Admin (not your own Admin; audited).");
    }

    private static async Task<IResult> List(string? q, string? role, int? page, int? pageSize, AdminUserService admin, CancellationToken ct) =>
        ApiResults.Page(await admin.ListAsync(q, role, new PageRequest(page, pageSize), ct));

    private static async Task<IResult> Grant(Guid userId, string role, AdminUserService admin, ICurrentUser user, CancellationToken ct) =>
        (await admin.SetRoleAsync(user.RequireUserId(), userId, role, grant: true, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Revoke(Guid userId, string role, AdminUserService admin, ICurrentUser user, CancellationToken ct) =>
        (await admin.SetRoleAsync(user.RequireUserId(), userId, role, grant: false, ct)).ToResult(ApiResults.Ok);
}
