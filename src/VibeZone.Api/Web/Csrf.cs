using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using VibeZone.Infrastructure.Security;
using VibeZone.Infrastructure.Web;

namespace VibeZone.Api.Web;

public sealed record CsrfTokenResponse(string Token);

/// <summary>
/// Antiforgery for the cookie-authenticated JSON API (ADR-0002): every unsafe method under /api must
/// send the request token in <see cref="HeaderName"/>, matching the antiforgery cookie. Tokens are bound
/// to the signed-in identity, so clients fetch a new one after login/logout.
/// </summary>
internal static class Csrf
{
    public const string HeaderName = "X-CSRF-TOKEN";
    public const string CookieBaseName = "vz_af";

    public static IServiceCollection AddVibeZoneAntiforgery(this IServiceCollection services, CookieSecurity security) =>
        services.AddAntiforgery(o =>
        {
            o.HeaderName = HeaderName;
            o.Cookie.Name = security.Name(CookieBaseName);
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = security.Policy;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.Path = "/";
            o.SuppressXFrameOptionsHeader = true; // SecurityHeaders already sends DENY
        });

    public static RouteGroupBuilder RequireCsrfForUnsafeMethods(this RouteGroupBuilder group) =>
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var method = http.Request.Method;
            // Bearer requests are authenticated by the header only (cookies are ignored for them) and a
            // cross-site page cannot set that header without CORS, which is not enabled (ADR-0007).
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method) ||
                BearerToken.IsPresent(http.Request) || http.GetEndpoint()?.Metadata.GetMetadata<CsrfExemptMetadata>() is not null)
            {
                return await next(context);
            }

            try
            {
                await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return TypedResults.Problem(
                    title: "Missing or invalid CSRF token.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["code"] = "csrf_invalid" });
            }

            return await next(context);
        });

    public static void MapCsrfEndpoint(this IEndpointRouteBuilder api) =>
        api.MapGet("/auth/csrf", (IAntiforgery antiforgery, HttpContext http) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(http);
                http.Response.Headers.CacheControl = "no-store";
                return TypedResults.Ok(new CsrfTokenResponse(tokens.RequestToken!));
            })
            .WithTags("Auth")
            .WithName("GetCsrfToken")
            .WithSummary("Issue an antiforgery token (and cookie) for subsequent POST/PUT/PATCH/DELETE requests.");
}
