using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Infrastructure.Security;
using VibeZone.Modules.Identity.Application;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>
/// Opaque access tokens protected with the shared Data Protection key ring (ADR-0007): no separate signing
/// secret to manage, and they only carry user id, session id and expiry — the session is checked per request.
/// </summary>
internal sealed class DataProtectionAccessTokens(IDataProtectionProvider provider) : IAccessTokens
{
    private const string Prefix = "vza_";
    private const string Version = "v1";
    private const int MaxLength = 1024;

    private readonly IDataProtector protector = provider.CreateProtector("VibeZone.Identity.AccessToken.v1");

    public TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(15);

    public string Issue(Guid userId, string handle, Guid sessionId, DateTimeOffset now)
    {
        var expires = (now + Lifetime).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return Prefix + protector.Protect($"{Version}|{userId:N}|{sessionId:N}|{expires}|{handle}");
    }

    public AccessTokenClaims? Read(string token, DateTimeOffset now)
    {
        if (token.Length > MaxLength || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string payload;
        try
        {
            payload = protector.Unprotect(token[Prefix.Length..]);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        var parts = payload.Split('|', 5);
        return parts.Length == 5 && parts[0] == Version &&
               Guid.TryParseExact(parts[1], "N", out var userId) &&
               Guid.TryParseExact(parts[2], "N", out var sessionId) &&
               long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) &&
               now.ToUnixTimeSeconds() < expires
            ? new AccessTokenClaims(userId, sessionId, parts[4])
            : null;
    }
}

internal sealed record AccessTokenClaims(Guid UserId, Guid SessionId, string Handle);

/// <summary><c>Authorization: Bearer vza_…</c> → principal, validated against the session store like the cookie.</summary>
internal sealed class AccessTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    DataProtectionAccessTokens tokens,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "vz-token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (BearerToken.Read(Request) is not { } token)
        {
            return AuthenticateResult.NoResult();
        }

        if (tokens.Read(token, time.GetUtcNow()) is not { } claims)
        {
            return AuthenticateResult.Fail("invalid_token");
        }

        var principal = await SessionAuthentication.AuthenticateSessionAsync(Context, claims.UserId, claims.Handle, claims.SessionId, SchemeName);
        return principal is null
            ? AuthenticateResult.Fail("invalid_token")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        // RFC 6750: tells the client to refresh (expired/revoked token) rather than re-prompt immediately.
        Response.Headers.WWWAuthenticate = BearerToken.IsPresent(Request) ? "Bearer error=\"invalid_token\"" : "Bearer";
        return Task.CompletedTask;
    }
}

/// <summary>Default scheme: bearer header → token handler, otherwise the session cookie. A request never uses both.</summary>
internal static class AuthenticationSelector
{
    public const string Scheme = "vz-auth";

    public static string Select(HttpContext context) =>
        BearerToken.IsPresent(context.Request) ? AccessTokenHandler.SchemeName : SessionAuthentication.Scheme;
}
