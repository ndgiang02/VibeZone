using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

/// <param name="DeviceName">Optional label shown in the sessions list (e.g. "Pixel 8"); defaults to the User-Agent.</param>
public sealed record TokenRequest(string? Email, string? Password, string? DeviceName);

public sealed record RefreshTokenRequest(string? RefreshToken);

/// <param name="ExpiresIn">Access token lifetime in seconds.</param>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Token sign-in for mobile/native clients (ADR-0007). Each token pair is a server-side session, so it
/// appears in the sessions list and is revoked by logout, "sign out other devices" and password changes.
/// </summary>
public sealed partial class TokenService(
    SessionService sessions,
    IUserSessions store,
    IUserAccounts accounts,
    IAccessTokens accessTokens,
    TimeProvider time,
    ILogger<TokenService> logger)
{
    internal static readonly DomainError InvalidRefreshToken =
        DomainError.Unauthorized("invalid_refresh_token", "The refresh token is invalid or expired. Sign in again.");

    public async Task<Result<TokenResponse>> IssueAsync(TokenRequest request, string? userAgent, CancellationToken cancellationToken)
    {
        var refresh = RefreshTokens.Create();
        var label = string.IsNullOrWhiteSpace(request.DeviceName) ? userAgent : request.DeviceName.Trim();
        var login = await sessions.LoginAsync(new LoginRequest(request.Email, request.Password, RememberMe: true), label, refresh.Hash, cancellationToken);
        if (!login.IsSuccess)
        {
            return login.Error;
        }

        var (account, session, _) = login.Value;
        return Build(account.Id, account.Handle, session.Id, session.ExpiresAt, refresh.Token);
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (RefreshTokens.Hash(request.RefreshToken) is not { } presented)
        {
            return InvalidRefreshToken;
        }

        var next = RefreshTokens.Create();
        var rotation = await store.RotateRefreshTokenAsync(presented, next.Hash, time.GetUtcNow(), cancellationToken);
        switch (rotation.Outcome)
        {
            case RefreshOutcome.Rotated:
                var account = await accounts.FindByIdAsync(rotation.UserId, cancellationToken);
                return account is null
                    ? InvalidRefreshToken
                    : Build(account.Id, account.Handle, rotation.SessionId, rotation.ExpiresAt, next.Token);
            case RefreshOutcome.Reused:
                LogReuse(logger, rotation.SessionId, rotation.UserId);
                return InvalidRefreshToken;
            default:
                return InvalidRefreshToken;
        }
    }

    /// <summary>Idempotent: unknown or already revoked tokens succeed too.</summary>
    public async Task RevokeAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (RefreshTokens.Hash(request.RefreshToken) is { } hash)
        {
            await store.RevokeByRefreshTokenAsync(hash, time.GetUtcNow(), cancellationToken);
        }
    }

    private TokenResponse Build(Guid userId, string handle, Guid sessionId, DateTimeOffset sessionExpiresAt, string refreshToken) =>
        new(
            accessTokens.Issue(userId, handle, sessionId, time.GetUtcNow()),
            "Bearer",
            (int)accessTokens.Lifetime.TotalSeconds,
            refreshToken,
            sessionExpiresAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rotated-out refresh token presented again; session {SessionId} of user {UserId} revoked")]
    private static partial void LogReuse(ILogger logger, Guid sessionId, Guid userId);
}

/// <summary>Opaque 256-bit refresh tokens. Only their SHA-256 is stored.</summary>
internal static class RefreshTokens
{
    private const string Prefix = "vzr_";
    private const int MaxLength = 128;

    public static (string Token, byte[] Hash) Create()
    {
        var token = Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token)!);
    }

    public static byte[]? Hash(string? token) =>
        token is { Length: <= MaxLength } && token.Length > Prefix.Length && token.StartsWith(Prefix, StringComparison.Ordinal)
            ? SHA256.HashData(Encoding.UTF8.GetBytes(token))
            : null;
}
