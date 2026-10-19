using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VibeZone.Infrastructure.Security;
using VibeZone.Modules.Identity.Domain;

namespace VibeZone.Modules.Identity.Infrastructure;

internal static class SessionAuthentication
{
    public const string Scheme = "vz-session";
    public const string CookieBaseName = "vz_session";

    /// <summary>The cookie carries only user id + session id; roles are loaded from the session store per request.</summary>
    public static ClaimsPrincipal CreatePrincipal(Guid userId, string handle, Guid sessionId, string scheme = Scheme) =>
        new(new ClaimsIdentity(
            [
                new Claim(VibeZoneClaims.UserId, userId.ToString()),
                new Claim(VibeZoneClaims.SessionId, sessionId.ToString()),
                new Claim(VibeZoneClaims.Handle, handle),
            ],
            scheme,
            VibeZoneClaims.Handle,
            ClaimTypes.Role));

    public static void Configure(CookieAuthenticationOptions options, CookieSecurity security)
    {
        options.Cookie.Name = security.Name(CookieBaseName);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = security.Policy;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        // API semantics: never redirect to an HTML login page.
        options.Events.OnRedirectToLogin = ctx => WriteStatus(ctx, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = ctx => WriteStatus(ctx, StatusCodes.Status403Forbidden);
        options.Events.OnValidatePrincipal = ValidateAsync;
    }

    /// <summary>
    /// Every authenticated request: the session must exist, belong to the user, be unrevoked and unexpired.
    /// Fresh roles are attached; last-seen is updated at most every few minutes.
    /// </summary>
    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirstValue(VibeZoneClaims.SessionId), out var sessionId) ||
            !Guid.TryParse(principal.FindFirstValue(VibeZoneClaims.UserId), out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var validated = await AuthenticateSessionAsync(context.HttpContext, userId, principal.FindFirstValue(VibeZoneClaims.Handle) ?? string.Empty, sessionId, Scheme);
        if (validated is null)
        {
            await RejectAsync(context);
            return;
        }

        context.ReplacePrincipal(validated);
    }

    /// <summary>
    /// Shared by cookie and bearer authentication: the session must be active and belong to the user.
    /// Returns the principal with fresh roles, or null.
    /// </summary>
    public static async Task<ClaimsPrincipal?> AuthenticateSessionAsync(HttpContext http, Guid userId, string handle, Guid sessionId, string scheme)
    {
        var services = http.RequestServices;
        var sessions = services.GetRequiredService<EfUserSessions>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var active = await sessions.GetActiveAsync(sessionId, userId, now, http.RequestAborted);
        if (active is null)
        {
            return null;
        }

        if (now - active.LastSeenAt >= UserSession.TouchInterval)
        {
            await sessions.TouchAsync(sessionId, now, http.RequestAborted);
        }

        var principal = CreatePrincipal(userId, handle, sessionId, scheme);
        ((ClaimsIdentity)principal.Identity!).AddClaims(active.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return principal;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(Scheme);
    }

    private static Task WriteStatus(RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}

/// <summary>Password-reset tokens get a short lifetime, separate from email-confirmation tokens (24 h).</summary>
internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    Microsoft.Extensions.Logging.ILogger<DataProtectorTokenProvider<AppUser>> logger)
    : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "PasswordReset";
}

internal sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(1);
    }
}
