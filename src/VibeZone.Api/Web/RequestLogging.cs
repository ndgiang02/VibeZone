using Serilog.Events;

namespace VibeZone.Api.Web;

internal static class RequestLogging
{
    /// <summary>Health probes at Verbose (dropped by default), client errors Information, server errors Error.</summary>
    public static LogEventLevel GetLevel(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        return context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    }
}
