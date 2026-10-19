using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Infrastructure;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Endpoints;

internal static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder api)
    {
        // Account endpoints that neither read nor set the session cookie are CSRF-exempt so native apps can
        // call them (ADR-0007); login/logout still require the token because they act on the cookie.
        var auth = api.MapGroup("/auth").WithTags("Auth")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/register", Register).DisableCsrf().WithName("Register").Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict).RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Create an account. Always 202 for valid input; a verification (or 'account exists') email follows.");
        auth.MapPost("/verify-email", VerifyEmail).DisableCsrf().WithName("VerifyEmail").Produces(StatusCodes.Status204NoContent).RequireRateLimiting(RateLimitPolicies.Auth);
        auth.MapPost("/resend-verification", ResendVerification).DisableCsrf().WithName("ResendVerification").Produces(StatusCodes.Status202Accepted).RequireRateLimiting(RateLimitPolicies.Auth);
        auth.MapPost("/login", Login).WithName("Login").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Sign in; sets the session cookie. Fetch a new CSRF token afterwards.");
        auth.MapPost("/logout", Logout).WithName("Logout").Produces(StatusCodes.Status204NoContent).WithSummary("Revokes the current session and clears the cookie. Idempotent.");

        // Mobile/native clients (ADR-0007): credentials in the body, tokens in the response, no cookies → no CSRF.
        var token = auth.MapGroup("/token").DisableCsrf();
        token.MapPost("", IssueToken).WithName("IssueToken").Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Sign in with email + password; returns an access token (Authorization: Bearer) and a rotating refresh token.");
        token.MapPost("/refresh", RefreshToken).WithName("RefreshToken").Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized).RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Exchange the refresh token for a new pair. Each refresh token works once; reusing one revokes the session.");
        token.MapPost("/revoke", RevokeToken).WithName("RevokeToken").Produces(StatusCodes.Status204NoContent)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Sign out a token session by its refresh token. Idempotent.");

        auth.MapPost("/forgot-password", ForgotPassword).DisableCsrf().WithName("ForgotPassword").Produces(StatusCodes.Status202Accepted).RequireRateLimiting(RateLimitPolicies.Auth);
        auth.MapPost("/reset-password", ResetPassword).DisableCsrf().WithName("ResetPassword").Produces(StatusCodes.Status204NoContent).RequireRateLimiting(RateLimitPolicies.Auth);
    }

    private static async Task<IResult> Register(RegisterRequest request, AccountService accounts, CancellationToken ct) =>
        (await accounts.RegisterAsync(request, ct)).ToResult(() => Results.Accepted());

    private static async Task<IResult> VerifyEmail(ConfirmEmailRequest request, AccountService accounts, CancellationToken ct) =>
        (await accounts.ConfirmEmailAsync(request, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> ResendVerification(EmailOnlyRequest request, AccountService accounts, CancellationToken ct) =>
        (await accounts.ResendVerificationAsync(request, ct)).ToResult(() => Results.Accepted());

    private static async Task<IResult> ForgotPassword(EmailOnlyRequest request, AccountService accounts, CancellationToken ct) =>
        (await accounts.ForgotPasswordAsync(request, ct)).ToResult(() => Results.Accepted());

    private static async Task<IResult> ResetPassword(ResetPasswordRequest request, AccountService accounts, CancellationToken ct) =>
        (await accounts.ResetPasswordAsync(request, ct)).ToResult(TypedResults.NoContent);

    private static async Task<IResult> Login(LoginRequest request, SessionService sessions, HttpContext http, CancellationToken ct)
    {
        var result = await sessions.LoginAsync(request, http.Request.Headers.UserAgent.ToString(), ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        var (account, session, persistent) = result.Value;
        await http.SignInAsync(
            SessionAuthentication.Scheme,
            SessionAuthentication.CreatePrincipal(account.Id, account.Handle, session.Id),
            new AuthenticationProperties { IsPersistent = persistent, AllowRefresh = true });
        return TypedResults.NoContent();
    }

    private static async Task<IResult> IssueToken(TokenRequest request, TokenService tokens, HttpContext http, CancellationToken ct) =>
        NoStore(http, await tokens.IssueAsync(request, http.Request.Headers.UserAgent.ToString(), ct));

    private static async Task<IResult> RefreshToken(RefreshTokenRequest request, TokenService tokens, HttpContext http, CancellationToken ct) =>
        NoStore(http, await tokens.RefreshAsync(request, ct));

    private static async Task<IResult> RevokeToken(RefreshTokenRequest request, TokenService tokens, CancellationToken ct)
    {
        await tokens.RevokeAsync(request, ct);
        return TypedResults.NoContent();
    }

    private static IResult NoStore(HttpContext http, Result<TokenResponse> result)
    {
        http.Response.Headers.CacheControl = "no-store";
        return result.ToResult(r => TypedResults.Ok(r));
    }

    private static async Task<IResult> Logout(SessionService sessions, ICurrentUser user, HttpContext http, CancellationToken ct)
    {
        if (user is { UserId: { } userId, SessionId: { } sessionId })
        {
            await sessions.LogoutAsync(userId, sessionId, ct);
        }

        await http.SignOutAsync(SessionAuthentication.Scheme);
        return TypedResults.NoContent();
    }
}
