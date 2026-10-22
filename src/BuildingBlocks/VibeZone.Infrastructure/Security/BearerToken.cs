using Microsoft.AspNetCore.Http;

namespace VibeZone.Infrastructure.Security;

/// <summary>Reads an <c>Authorization: Bearer</c> credential (mobile/native clients, ADR-0007).</summary>
public static class BearerToken
{
    private const string Prefix = "Bearer ";

    /// <summary>
    /// Realtime hubs: WebSocket/SSE clients can't set headers, so the token may come as <c>?access_token=</c>
    /// on these paths only (SignalR convention). Request logs record the path without the query string.
    /// </summary>
    private const string HubPathPrefix = "/api/v1/hubs/";

    /// <summary>True when the request carries a bearer credential; such requests never use cookies.</summary>
    public static bool IsPresent(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || Read(request) is not null;

    public static string? Read(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[Prefix.Length..].Trim() is { Length: > 0 } token ? token : null;
        }

        return request.Path.StartsWithSegments(HubPathPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
               request.Query["access_token"].ToString() is { Length: > 0 } queryToken
            ? queryToken
            : null;
    }
}
