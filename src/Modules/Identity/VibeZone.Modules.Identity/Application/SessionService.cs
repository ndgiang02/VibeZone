using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe);

public sealed record LoginResult(Account Account, UserSession Session, bool Persistent);

public sealed record SessionDto(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, string? UserAgent, bool IsCurrent);

public sealed class SessionService(IUserAccounts accounts, IUserSessions sessions, TimeProvider time)
{
    /// <summary>Unknown email, wrong password and lock-out all produce this same error.</summary>
    internal static readonly DomainError InvalidCredentials =
        DomainError.Unauthorized("invalid_credentials", "Email or password is incorrect.");

    public Task<Result<LoginResult>> LoginAsync(LoginRequest request, string? userAgent, CancellationToken cancellationToken) =>
        LoginAsync(request, userAgent, refreshTokenHash: null, cancellationToken);

    /// <summary>Same credential rules for cookie and token clients; token sessions also store a refresh token hash.</summary>
    internal async Task<Result<LoginResult>> LoginAsync(LoginRequest request, string? userAgent, byte[]? refreshTokenHash, CancellationToken cancellationToken)
    {
        var email = Validation.NormalizeEmail(request.Email);
        if (email.Length == 0 || string.IsNullOrEmpty(request.Password))
        {
            return InvalidCredentials;
        }

        var check = await accounts.CheckPasswordAsync(email, request.Password, cancellationToken);
        switch (check.Outcome)
        {
            case PasswordCheckOutcome.Succeeded:
                var session = UserSession.Start(check.Account!.Id, userAgent, time.GetUtcNow(), refreshTokenHash);
                await sessions.AddAsync(session, cancellationToken);
                return new LoginResult(check.Account, session, request.RememberMe);
            case PasswordCheckOutcome.EmailNotConfirmed:
                // Only reachable with the correct password, so it reveals nothing to a guesser.
                return DomainError.Forbidden("email_not_confirmed", "Confirm your email address before signing in.");
            default:
                return InvalidCredentials;
        }
    }

    public Task LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        sessions.RevokeAsync(userId, sessionId, time.GetUtcNow(), cancellationToken);

    public async Task<IReadOnlyList<SessionDto>> ListAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken)
    {
        var active = await sessions.ListActiveAsync(userId, time.GetUtcNow(), cancellationToken);
        return active
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionDto(s.Id, s.CreatedAt, s.LastSeenAt, s.UserAgent, s.Id == currentSessionId))
            .ToList();
    }

    public async Task<Result> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        await sessions.RevokeAsync(userId, sessionId, time.GetUtcNow(), cancellationToken)
            ? Result.Success()
            : Result.Failure(DomainError.NotFound("session_not_found", "Session not found."));

    public Task<int> RevokeOthersAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken) =>
        sessions.RevokeAllAsync(userId, currentSessionId, time.GetUtcNow(), cancellationToken);
}
