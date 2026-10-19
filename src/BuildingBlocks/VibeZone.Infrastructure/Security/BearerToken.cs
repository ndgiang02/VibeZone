using Microsoft.AspNetCore.Http;

namespace VibeZone.Infrastructure.Security;

/// <summary>Reads an <c>Authorization: Bearer</c> credential (mobile/native clients, ADR-0007).</summary>
public static class BearerToken
{
    private const string Prefix = "Bearer ";

    /// <summary>True when the request carries a bearer credential; such requests never use cookies.</summary>
    public static bool IsPresent(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string? Read(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && header[Prefix.Length..].Trim() is { Length: > 0 } token ? token : null;
    }
}
