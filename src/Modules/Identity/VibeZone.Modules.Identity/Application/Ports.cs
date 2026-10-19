using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

public sealed record Account(Guid Id, string Email, string Handle, bool EmailConfirmed);

public sealed record NewAccount(string Email, string Handle, string Password, string DisplayName, string Locale);

public enum PasswordCheckOutcome
{
    Succeeded,
    Invalid,
    LockedOut,
    EmailNotConfirmed,
}

public sealed record PasswordCheck(PasswordCheckOutcome Outcome, Account? Account);

/// <summary>Credential store (ASP.NET Core Identity in Infrastructure).</summary>
public interface IUserAccounts
{
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Account?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Creates the user and its profile atomically.</summary>
    Task<Result<Account>> CreateAsync(NewAccount account, CancellationToken cancellationToken);

    /// <summary>Spends the same CPU as hashing a real password, so "email already registered" isn't observable by timing.</summary>
    void BurnPasswordHash(string password);

    Task<PasswordCheck> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken);

    Task<string> CreateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);

    Task<string> CreatePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IUserProfiles
{
    Task<UserProfile?> FindAsync(Guid userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserSessions
{
    Task AddAsync(UserSession session, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Revokes one of the user's active sessions. False if it doesn't exist, isn't theirs, or is already inactive.</summary>
    Task<bool> RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<int> RevokeAllAsync(Guid userId, Guid? exceptSessionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically replaces the session's current refresh token. A token that was already rotated out revokes
    /// its session (<see cref="RefreshOutcome.Reused"/>).
    /// </summary>
    Task<RefreshRotation> RotateRefreshTokenAsync(byte[] presentedHash, byte[] nextHash, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Revokes the active session holding this refresh token, if any.</summary>
    Task<bool> RevokeByRefreshTokenAsync(byte[] hash, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum RefreshOutcome
{
    Rotated,
    Invalid,
    Reused,
}

public sealed record RefreshRotation(RefreshOutcome Outcome, Guid SessionId = default, Guid UserId = default, DateTimeOffset ExpiresAt = default);

/// <summary>Short-lived access tokens for bearer clients (Data Protection in Infrastructure, ADR-0007).</summary>
public interface IAccessTokens
{
    TimeSpan Lifetime { get; }

    string Issue(Guid userId, string handle, Guid sessionId, DateTimeOffset now);
}

/// <summary>Transactional emails, localized by the implementation.</summary>
public interface IAccountEmails
{
    Task SendEmailVerificationAsync(Account account, string locale, string token, CancellationToken cancellationToken);

    Task SendAccountExistsAsync(string email, string locale, CancellationToken cancellationToken);

    Task SendPasswordResetAsync(Account account, string locale, string token, CancellationToken cancellationToken);

    Task SendPasswordChangedAsync(Account account, string locale, CancellationToken cancellationToken);
}

/// <summary>Caps how many emails of one kind a single address can trigger (anti-spam, anti-abuse).</summary>
public interface IEmailSendThrottle
{
    bool TryAcquire(string purpose, string email);
}
