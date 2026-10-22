using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Analytics.Application;
using VibeZone.Modules.Analytics.Contracts;
using VibeZone.Modules.Analytics.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Analytics.Infrastructure;

// track_id references catalog.tracks and user_id identity.users; those FKs are added in SQL by the migration
// because the referenced models belong to other modules (ADR-0001).

internal sealed class PlayEventConfiguration : IEntityTypeConfiguration<PlayEvent>
{
    public void Configure(EntityTypeBuilder<PlayEvent> b)
    {
        b.ToTable("play_events", AnalyticsModule.Schema, t => t.HasCheckConstraint("ck_play_events_type", $"type IN ({EnumCheck.Values<PlayEventType>()})"));
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Type).HasConversion(new LowerCaseEnumConverter<PlayEventType>()).HasMaxLength(16);
        b.HasIndex(e => new { e.TrackId, e.ReceivedAt });
        b.HasIndex(e => e.SessionId);
    }
}

internal sealed class PlaySessionConfiguration : IEntityTypeConfiguration<PlaySession>
{
    public void Configure(EntityTypeBuilder<PlaySession> b)
    {
        b.ToTable("play_history", AnalyticsModule.Schema);
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.ListenerKey).HasMaxLength(PlaySession.MaxListenerKeyLength).IsRequired();
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(s => new { s.UserId, s.StartedAt }).IsDescending(false, true).HasFilter("user_id IS NOT NULL");
        b.HasIndex(s => new { s.ListenerKey, s.TrackId, s.ValidAt }).HasDatabaseName("ix_play_history_valid_listens").HasFilter("is_valid_listen");
    }
}

internal sealed class TrackDailyStatConfiguration : IEntityTypeConfiguration<TrackDailyStat>
{
    public void Configure(EntityTypeBuilder<TrackDailyStat> b)
    {
        b.ToTable("track_daily_stats", AnalyticsModule.Schema, t => t.HasCheckConstraint("ck_track_daily_stats_counts", "plays >= 0 AND valid_listens >= 0 AND likes >= 0"));
        b.HasKey(s => new { s.TrackId, s.Day });
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(s => s.Day);
    }
}

internal sealed class EfAnalyticsRepository(AppDbContext db) : IAnalyticsRepository
{
    public async Task<IReadOnlySet<Guid>> ExistingEventIdsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken) =>
        (await db.Set<PlayEvent>().AsNoTracking().Where(e => eventIds.Contains(e.Id)).Select(e => e.Id).ToListAsync(cancellationToken)).ToHashSet();

    public Task<PlaySession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        db.Set<PlaySession>().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public Task<bool> HasValidListenSinceAsync(string listenerKey, Guid trackId, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.Set<PlaySession>().AnyAsync(s => s.ListenerKey == listenerKey && s.TrackId == trackId && s.IsValidListen && s.ValidAt >= since, cancellationToken);

    public async Task<TrackDailyStat> GetOrAddDailyStatAsync(Guid trackId, DateOnly day, CancellationToken cancellationToken)
    {
        var stat = db.Set<TrackDailyStat>().Local.FirstOrDefault(s => s.TrackId == trackId && s.Day == day)
            ?? await db.Set<TrackDailyStat>().SingleOrDefaultAsync(s => s.TrackId == trackId && s.Day == day, cancellationToken);
        if (stat is null)
        {
            stat = new TrackDailyStat(trackId, day);
            db.Set<TrackDailyStat>().Add(stat);
        }

        return stat;
    }

    public async Task<TrackStatsDto> GetTotalsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var totals = await db.Set<TrackDailyStat>().AsNoTracking().Where(s => s.TrackId == trackId)
            .GroupBy(s => s.TrackId)
            .Select(g => new { Plays = g.Sum(s => (long)s.Plays), Listens = g.Sum(s => (long)s.ValidListens) })
            .SingleOrDefaultAsync(cancellationToken);
        return new TrackStatsDto(totals?.Plays ?? 0, totals?.Listens ?? 0);
    }

    public void Add(PlayEvent playEvent) => db.Set<PlayEvent>().Add(playEvent);

    public void Add(PlaySession session) => db.Set<PlaySession>().Add(session);

    public async Task SaveAsync(IReadOnlyCollection<StatIncrement> increments, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var i in increments)
        {
            // Upsert: concurrent batches for the same track add up instead of conflicting.
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO analytics.track_daily_stats (track_id, day, plays, valid_listens, likes)
                VALUES ({i.TrackId}, {i.Day}, {i.Plays}, {i.ValidListens}, 0)
                ON CONFLICT (track_id, day) DO UPDATE
                SET plays = track_daily_stats.plays + EXCLUDED.plays,
                    valid_listens = track_daily_stats.valid_listens + EXCLUDED.valid_listens
                """, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

internal sealed class EfListeningHistory(AppDbContext db) : IListeningHistory
{
    public async Task<Paged<PlayedTrack>> PageRecentAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var recent = db.Set<PlaySession>().AsNoTracking()
            .Where(s => s.UserId == userId)
            .GroupBy(s => s.TrackId)
            .Select(g => new { TrackId = g.Key, PlayedAt = g.Max(s => s.StartedAt) });
        var total = await recent.CountAsync(cancellationToken);
        var rows = total <= page.Skip
            ? []
            : await recent.OrderByDescending(r => r.PlayedAt).ThenBy(r => r.TrackId).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<PlayedTrack>(rows.Select(r => new PlayedTrack(r.TrackId, r.PlayedAt)).ToList(), page.Page, page.PageSize, total);
    }
}

internal sealed class EfTrendingTracks(AppDbContext db, TimeProvider time) : ITrendingTracks
{
    public const int Days = 7;

    public async Task<IReadOnlyList<Guid>> TopAsync(int limit, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var from = today.AddDays(-(Days - 1));
        // Score: valid listens + 2 × likes, weighted 7 (today) down to 1 (six days ago). Plays break ties.
        return await db.Database.SqlQuery<Guid>($"""
            SELECT track_id AS "Value" FROM analytics.track_daily_stats
            WHERE day >= {from}
            GROUP BY track_id
            HAVING sum(plays + valid_listens + likes) > 0
            ORDER BY sum((valid_listens + 2 * likes) * ({Days} - ({today} - day))) DESC, sum(plays) DESC, track_id
            LIMIT {limit}
            """).ToListAsync(cancellationToken);
    }
}
