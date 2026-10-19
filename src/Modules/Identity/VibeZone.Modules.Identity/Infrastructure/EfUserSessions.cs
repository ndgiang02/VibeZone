using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Domain;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>A validated session as seen by request authentication (cached briefly).</summary>
internal sealed record ActiveSession(Guid SessionId, Guid UserId, DateTimeOffset ExpiresAt, DateTimeOffset LastSeenAt, IReadOnlyList<string> Roles);

internal sealed class EfUserSessions(AppDbContext db, IMemoryCache cache) : IUserSessions
{
    /// <summary>
    /// Per-instance cache of session state used on every authenticated request. Revocations made on this
    /// instance evict immediately; other instances notice within this window (Redis-backed in multi-instance setups).
    /// </summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public static string CacheKey(Guid sessionId) => $"identity:session:{sessionId}";

    public async Task AddAsync(UserSession session, CancellationToken cancellationToken)
    {
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSession>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Set<UserSession>().AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancellationToken);

    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var revoked = await db.Set<UserSession>()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, now), cancellationToken);
        cache.Remove(CacheKey(sessionId));
        return revoked > 0;
    }

    public async Task<int> RevokeAllAsync(Guid userId, Guid? exceptSessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = await db.Set<UserSession>()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var revoked = await db.Set<UserSession>()
            .Where(s => ids.Contains(s.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, now), cancellationToken);
        foreach (var id in ids)
        {
            cache.Remove(CacheKey(id));
        }

        return revoked;
    }

    public async Task<RefreshRotation> RotateRefreshTokenAsync(byte[] presentedHash, byte[] nextHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await db.Set<UserSession>().AsNoTracking()
            .Where(s => s.RefreshTokenHash == presentedHash && s.RevokedAt == null && s.ExpiresAt > now)
            .Select(s => new { s.Id, s.UserId, s.ExpiresAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (current is not null)
        {
            // Compare-and-swap on the hash: of two concurrent refreshes with one token, only one wins.
            var rotated = await db.Set<UserSession>()
                .Where(s => s.Id == current.Id && s.RefreshTokenHash == presentedHash && s.RevokedAt == null)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(s => s.PreviousRefreshTokenHash, presentedHash)
                        .SetProperty(s => s.RefreshTokenHash, nextHash)
                        .SetProperty(s => s.LastSeenAt, now),
                    cancellationToken);
            if (rotated == 1)
            {
                return new RefreshRotation(RefreshOutcome.Rotated, current.Id, current.UserId, current.ExpiresAt);
            }
        }

        // A token that was already exchanged: either it leaked or the client lost the race above.
        // Either way the whole session is revoked and the user signs in again (ADR-0007).
        var reused = await db.Set<UserSession>().AsNoTracking()
            .Where(s => s.PreviousRefreshTokenHash == presentedHash && s.RevokedAt == null)
            .Select(s => new { s.Id, s.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (reused is null)
        {
            return new RefreshRotation(RefreshOutcome.Invalid);
        }

        await RevokeAsync(reused.UserId, reused.Id, now, cancellationToken);
        return new RefreshRotation(RefreshOutcome.Reused, reused.Id, reused.UserId);
    }

    public async Task<bool> RevokeByRefreshTokenAsync(byte[] hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await db.Set<UserSession>().AsNoTracking()
            .Where(s => s.RefreshTokenHash == hash && s.RevokedAt == null)
            .Select(s => new { s.Id, s.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        return session is not null && await RevokeAsync(session.UserId, session.Id, now, cancellationToken);
    }

    /// <summary>Session lookup for request authentication; null when unknown, revoked or expired.</summary>
    public async Task<ActiveSession?> GetActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(CacheKey(sessionId), out ActiveSession? active))
        {
            var session = await db.Set<UserSession>().AsNoTracking()
                .Where(s => s.Id == sessionId && s.RevokedAt == null)
                .Select(s => new { s.UserId, s.ExpiresAt, s.LastSeenAt })
                .SingleOrDefaultAsync(cancellationToken);

            active = session is null
                ? null
                : new ActiveSession(sessionId, session.UserId, session.ExpiresAt, session.LastSeenAt, await RoleQueries.ForUserAsync(db, session.UserId, cancellationToken));

            cache.Set(CacheKey(sessionId), active, CacheDuration);
        }

        return active is not null && active.UserId == userId && now < active.ExpiresAt ? active : null;
    }

    public async Task TouchAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.Set<UserSession>()
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastSeenAt, now), cancellationToken);
        if (cache.TryGetValue(CacheKey(sessionId), out ActiveSession? active) && active is not null)
        {
            cache.Set(CacheKey(sessionId), active with { LastSeenAt = now }, CacheDuration);
        }
    }

    /// <summary>Evicts cached sessions of a user, e.g. after a role change, so new roles apply immediately on this instance.</summary>
    public async Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ids = await db.Set<UserSession>().Where(s => s.UserId == userId && s.RevokedAt == null).Select(s => s.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            cache.Remove(CacheKey(id));
        }
    }
}

internal static class RoleQueries
{
    public static async Task<IReadOnlyList<string>> ForUserAsync(AppDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == userId)
            .Join(db.Set<AppRole>(), ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);
}

internal sealed class EfUserProfiles(AppDbContext db) : IUserProfiles
{
    public Task<UserProfile?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Set<UserProfile>().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
