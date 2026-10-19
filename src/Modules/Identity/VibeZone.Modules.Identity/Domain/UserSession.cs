using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Domain;

/// <summary>
/// A server-side login session. The auth cookie only carries its id, so sessions can be listed and
/// revoked (logout, "sign out other devices", password change) — see ADR-0002.
/// </summary>
public sealed class UserSession
{
    public const int MaxUserAgentLength = 256;

    /// <summary>Absolute lifetime regardless of activity.</summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);

    /// <summary>How stale <see cref="LastSeenAt"/> may get before it is written again.</summary>
    public static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    private UserSession() { }

    /// <param name="refreshTokenHash">SHA-256 of the refresh token for token (mobile) sessions; null for cookie sessions.</param>
    public static UserSession Start(Guid userId, string? userAgent, DateTimeOffset now, byte[]? refreshTokenHash = null) => new()
    {
        Id = EntityId.New(),
        UserId = userId,
        CreatedAt = now,
        LastSeenAt = now,
        ExpiresAt = now + AbsoluteLifetime,
        UserAgent = Truncate(userAgent),
        RefreshTokenHash = refreshTokenHash,
    };

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>Hash of the current refresh token (token sessions only, ADR-0007). Rotated on every refresh.</summary>
    public byte[]? RefreshTokenHash { get; private set; }

    /// <summary>Hash of the refresh token rotated out last; presenting it again revokes the session (reuse detection).</summary>
    public byte[]? PreviousRefreshTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    public bool NeedsTouch(DateTimeOffset now) => now - LastSeenAt >= TouchInterval;

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= MaxUserAgentLength ? value : value[..MaxUserAgentLength];
}
