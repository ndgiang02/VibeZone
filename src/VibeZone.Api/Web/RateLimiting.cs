using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VibeZone.Infrastructure.Web;

namespace VibeZone.Api.Web;

internal static class RateLimiting
{
    /// <summary>
    /// Coarse global per-IP limit plus a strict per-IP policy for credential/email endpoints
    /// (in-memory, per instance). Upload and play-event policies are added with those features.
    /// </summary>
    public static IServiceCollection AddVibeZoneRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue("RateLimiting:GlobalPermitsPerMinute", 600);
        var authPermitsPerMinute = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
        var uploadsPerHour = configuration.GetValue("RateLimiting:UploadsPerHour", 30);
        var socialPermitsPerMinute = configuration.GetValue("RateLimiting:SocialPermitsPerMinute", 60);
        var playBatchesPerMinute = configuration.GetValue("RateLimiting:PlayBatchesPerMinute", 60);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RateLimitPolicies.Auth, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RateLimitPolicies.Uploads, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = uploadsPerHour,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RateLimitPolicies.Social, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PerUserKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = socialPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RateLimitPolicies.Plays, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PerUserKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = playBatchesPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (ctx, cancellationToken) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var problems = ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = ctx.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests." },
                });
            };
        });

        return services;
    }

    /// <summary>Signed-in callers are limited per account (many users can share an IP), anonymous ones per IP.</summary>
    private static string PerUserKey(HttpContext ctx) =>
        ctx.User.FindFirst(VibeZone.Infrastructure.Security.VibeZoneClaims.UserId)?.Value is { } userId
            ? "u:" + userId
            : "ip:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
