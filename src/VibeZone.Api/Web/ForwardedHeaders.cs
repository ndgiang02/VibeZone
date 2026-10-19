using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace VibeZone.Api.Web;

internal static class ForwardedHeaders
{
    private const string Section = "ReverseProxy:KnownProxies";

    /// <summary>
    /// Trust <c>X-Forwarded-For/Proto</c> only from explicitly configured proxy IPs, so rate limiting
    /// and logs see the real client. Without configuration, forwarded headers are ignored.
    /// </summary>
    public static IServiceCollection AddVibeZoneForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection(Section).Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
            {
                o.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        return services;
    }

    public static IApplicationBuilder UseVibeZoneForwardedHeaders(this WebApplication app)
    {
        if ((app.Configuration.GetSection(Section).Get<string[]>() ?? []).Length > 0)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
