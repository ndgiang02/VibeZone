using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;

namespace VibeZone.Infrastructure.Outbox;

/// <param name="Pending">Waiting for dispatch or retrying.</param>
/// <param name="DeadLettered">Gave up after the maximum attempts; needs an operator.</param>
/// <param name="ProcessedLastDay">Dispatched in the last 24 hours.</param>
public sealed record OutboxSummaryDto(int Pending, int DeadLettered, int ProcessedLastDay);

public static class OutboxEndpoints
{
    public static void MapOutboxAdminEndpoints(this IEndpointRouteBuilder api) =>
        api.MapGet("/admin/outbox", async (AppDbContext db, TimeProvider time, CancellationToken ct) =>
            {
                var messages = db.Set<OutboxMessage>().AsNoTracking();
                var since = time.GetUtcNow().AddDays(-1);
                return ApiResults.Ok(new OutboxSummaryDto(
                    await messages.CountAsync(m => m.ProcessedAt == null && m.Attempts < OutboxDispatcher.MaxAttempts, ct),
                    await messages.CountAsync(m => m.ProcessedAt == null && m.Attempts >= OutboxDispatcher.MaxAttempts, ct),
                    await messages.CountAsync(m => m.ProcessedAt >= since, ct)));
            })
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .WithTags("Admin")
            .WithName("AdminOutboxSummary")
            .Produces<ApiResponse<OutboxSummaryDto>>()
            .WithSummary("Admins. Integration-event outbox: pending, dead-lettered and recently processed messages.");
}
