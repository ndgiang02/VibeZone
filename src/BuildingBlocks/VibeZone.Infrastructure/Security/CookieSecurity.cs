using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace VibeZone.Infrastructure.Security;

/// <summary>
/// Cookie hardening for the session and antiforgery cookies. Secure (default): <c>__Host-</c> prefix and
/// the Secure flag — requires HTTPS end to end or a trusted TLS-terminating proxy (ReverseProxy:KnownProxies).
/// <c>Security:SecureCookies=false</c> exists only for plain-HTTP local development (http://localhost);
/// ASP.NET antiforgery throws when asked to issue a Secure cookie on a non-HTTPS request, and some browsers
/// (e.g. Safari) don't treat http://localhost as a secure context.
/// </summary>
public sealed record CookieSecurity(bool Secure)
{
    public const string ConfigurationKey = "Security:SecureCookies";

    public static CookieSecurity From(IConfiguration configuration) => new(configuration.GetValue(ConfigurationKey, true));

    public string Name(string baseName) => Secure ? $"__Host-{baseName}" : baseName;

    public CookieSecurePolicy Policy => Secure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
}
