using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Moderation.Application;
using VibeZone.Modules.Moderation.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Moderation.Endpoints;

internal static class ModerationEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        api.MapPost("/reports", Create).WithTags("Moderation").WithName("ReportContent").RequireAuthorization().RequireRateLimiting(RateLimitPolicies.Social)
            .Produces<ApiResponse<MyReportDto>>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Report a track, comment or playlist you can see (reason: copyright, abuse, spam, other). One open report per item.");

        var moderation = api.MapGroup("/moderation").WithTags("Moderation").RequireAuthorization(AuthorizationPolicies.Moderator);
        moderation.MapGet("/reports", List).WithName("ListReports").Produces<ApiListResponse<ReportDto>>()
            .WithSummary("Moderators. status=open (oldest first, the queue), actioned or rejected (newest first); omit for all.");
        moderation.MapPost("/reports/{reportId:guid}/resolve", Resolve).WithName("ResolveReport")
            .Produces<ApiResponse<ReportDto>>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Moderators. action=remove takes the content down (resolving every open report on it) or dismiss. Audited; reporter and owner are notified.");
        moderation.MapPost("/content/{subjectType}/{subjectId:guid}/restore", Restore).WithName("RestoreContent")
            .Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Moderators. Lifts a takedown of a track or playlist. Audited.");

        api.MapGet("/admin/audit-log", ListAudit).WithTags("Admin").WithName("ListAuditLog").RequireAuthorization(AuthorizationPolicies.Admin)
            .Produces<ApiListResponse<AuditEntryDto>>()
            .WithSummary("Admins. Staff actions, newest first (paged).");
    }

    private static async Task<IResult> Create(CreateReportRequest request, ReportService reports, ICurrentUser user, CancellationToken ct) =>
        (await reports.CreateAsync(user.RequireUserId(), request, ct)).ToResult(r => ApiResults.Created($"/api/v1/reports/{r.Id}", r));

    private static async Task<IResult> List(ReportStatus? status, int? page, int? pageSize, ReportService reports, CancellationToken ct) =>
        ApiResults.Page(await reports.ListAsync(status, new PageRequest(page, pageSize), ct));

    private static async Task<IResult> Resolve(Guid reportId, ResolveReportRequest request, ReportService reports, ICurrentUser user, CancellationToken ct) =>
        (await reports.ResolveAsync(user.RequireUserId(), reportId, request, ct)).ToResult(ApiResults.Ok);

    private static async Task<IResult> Restore(ReportSubjectType subjectType, Guid subjectId, ReportService reports, ICurrentUser user, CancellationToken ct) =>
        (await reports.RestoreAsync(user.RequireUserId(), subjectType, subjectId, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> ListAudit(int? page, int? pageSize, ReportService reports, CancellationToken ct) =>
        ApiResults.Page(await reports.ListAuditAsync(new PageRequest(page, pageSize), ct));
}
