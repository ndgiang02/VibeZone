using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Security;

public static class VibeZoneClaims
{
    public const string UserId = "sub";
    public const string SessionId = "sid";
    public const string Handle = "handle";
}

/// <summary>Reads the caller from the authenticated principal (validated against the session store).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => ReadGuid(VibeZoneClaims.UserId);

    public Guid? SessionId => ReadGuid(VibeZoneClaims.SessionId);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    private Guid? ReadGuid(string claimType) =>
        Principal?.Identity?.IsAuthenticated == true && Guid.TryParse(Principal.FindFirstValue(claimType), out var id) ? id : null;
}
