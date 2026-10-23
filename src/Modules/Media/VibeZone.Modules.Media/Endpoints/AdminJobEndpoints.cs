using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Endpoints;

internal static class AdminJobEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        var jobs = api.MapGroup("/admin/jobs").WithTags("Admin").RequireAuthorization(AuthorizationPolicies.Admin);
        jobs.MapGet("/", List).WithName("AdminListJobs").Produces<ApiListResponse<AdminJobDto>>()
            .WithSummary("Admins. Media processing jobs, newest first (paged), optionally by status.");
        jobs.MapGet("/summary", Summary).WithName("AdminJobSummary").Produces<ApiResponse<JobSummaryDto>>()
            .WithSummary("Admins. Number of jobs per status.");
    }

    private static async Task<IResult> List(UploadJobStatus? status, int? page, int? pageSize, JobAdminService jobs, CancellationToken ct) =>
        ApiResults.Page(await jobs.ListAsync(status, new PageRequest(page, pageSize), ct));

    private static async Task<IResult> Summary(JobAdminService jobs, CancellationToken ct) =>
        ApiResults.Ok(await jobs.SummaryAsync(ct));
}
