using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using VibeZone.Infrastructure.Persistence;

namespace VibeZone.Infrastructure.Health;

/// <summary>Ready only when PostgreSQL is reachable and every migration has been applied.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending} pending migration(s).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Details stay in logs; the probe response only says "Unhealthy".
            logger.LogWarning(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
    }
}
