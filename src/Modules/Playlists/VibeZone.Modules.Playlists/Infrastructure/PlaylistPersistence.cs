using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Playlists.Application;
using VibeZone.Modules.Playlists.Contracts;
using VibeZone.Modules.Playlists.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Playlists.Infrastructure;

// owner_user_id references identity.users and playlist_tracks.track_id references catalog.tracks; those FKs
// are added in SQL by the migration because the referenced models belong to other modules (ADR-0001).

internal sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> b)
    {
        b.ToTable("playlists", PlaylistsModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_playlists_visibility", $"visibility IN ({EnumCheck.Values<PlaylistVisibility>()})");
            t.HasCheckConstraint("ck_playlists_track_count", "track_count >= 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Title).HasMaxLength(Playlist.MaxTitleLength).IsRequired();
        b.Property(p => p.Description).HasMaxLength(Playlist.MaxDescriptionLength);
        b.Property(p => p.Visibility).HasConversion(new LowerCaseEnumConverter<PlaylistVisibility>()).HasMaxLength(16);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasMany(p => p.Tracks).WithOne().HasForeignKey(t => t.PlaylistId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Tracks).HasField("_tracks").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(p => new { p.OwnerUserId, p.UpdatedAt }).IsDescending(false, true);
        b.HasIndex(p => p.UpdatedAt).IsDescending().HasDatabaseName("ix_playlists_public").HasFilter("visibility = 'public' AND track_count > 0 AND taken_down_at IS NULL");
    }
}

internal sealed class PlaylistTrackConfiguration : IEntityTypeConfiguration<PlaylistTrack>
{
    public void Configure(EntityTypeBuilder<PlaylistTrack> b)
    {
        b.ToTable("playlist_tracks", PlaylistsModule.Schema, t => t.HasCheckConstraint("ck_playlist_tracks_position", "position > 0"));
        b.HasKey(t => new { t.PlaylistId, t.TrackId });
        b.HasIndex(t => new { t.PlaylistId, t.Position });
        b.HasIndex(t => t.TrackId);
    }
}

internal sealed class EfPlaylistRepository(AppDbContext db) : IPlaylistRepository
{
    public Task<Playlist?> FindAsync(Guid playlistId, CancellationToken cancellationToken) =>
        db.Set<Playlist>().Include(p => p.Tracks).SingleOrDefaultAsync(p => p.Id == playlistId, cancellationToken);

    public Task<Paged<Playlist>> PageByOwnerAsync(Guid ownerUserId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<Playlist>().AsNoTracking().Where(p => p.OwnerUserId == ownerUserId).OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id), page, cancellationToken);

    public async Task<Paged<Playlist>> PagePublicAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var listed = db.Set<Playlist>().AsNoTracking().Where(p => p.Visibility == PlaylistVisibility.Public && p.TrackCount > 0 && p.TakenDownAt == null);
        if (query is null)
        {
            return await PageAsync(listed.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id), page, cancellationToken);
        }

        // Accent-insensitive title match (search_vector + trigram, migration AddPlaylists); ranked in SQL.
        var raw = query.Length > 100 ? query[..100] : query;
        var terms = string.Join(" & ", System.Text.RegularExpressions.Regex.Matches(raw, @"[\p{L}\p{N}]+").Select(m => m.Value.ToLowerInvariant() + ":*").Take(8));
        if (terms.Length == 0)
        {
            return new Paged<Playlist>([], page.Page, page.PageSize, 0);
        }

        var total = await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM playlists.playlists p
            WHERE p.visibility = 'public' AND p.track_count > 0 AND p.taken_down_at IS NULL
              AND (p.search_vector @@ to_tsquery('simple', public.vz_fold({terms}))
                OR public.vz_fold({raw}) <% public.vz_fold(p.title))
            """).SingleAsync(cancellationToken);
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT p.id AS "Value" FROM playlists.playlists p
            WHERE p.visibility = 'public' AND p.track_count > 0 AND p.taken_down_at IS NULL
              AND (p.search_vector @@ to_tsquery('simple', public.vz_fold({terms}))
                OR public.vz_fold({raw}) <% public.vz_fold(p.title))
            ORDER BY ts_rank(p.search_vector, to_tsquery('simple', public.vz_fold({terms})))
                   + word_similarity(public.vz_fold({raw}), public.vz_fold(p.title)) DESC, p.updated_at DESC, p.id
            LIMIT {page.PageSize} OFFSET {page.Skip}
            """).ToListAsync(cancellationToken);
        var rows = (await db.Set<Playlist>().AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken)).ToDictionary(p => p.Id);
        return new Paged<Playlist>(ids.Select(id => rows[id]).ToList(), page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> FirstTrackIdsAsync(IReadOnlyCollection<Guid> playlistIds, int perPlaylist, CancellationToken cancellationToken)
    {
        if (playlistIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var rows = await db.Set<PlaylistTrack>().AsNoTracking()
            .Where(t => playlistIds.Contains(t.PlaylistId) && t.Position <= perPlaylist)
            .OrderBy(t => t.Position)
            .Select(t => new { t.PlaylistId, t.TrackId })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.PlaylistId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(r => r.TrackId).ToList());
    }

    public async Task<IReadOnlySet<Guid>> PlaylistsContainingAsync(IReadOnlyCollection<Guid> playlistIds, Guid trackId, CancellationToken cancellationToken) =>
        (await db.Set<PlaylistTrack>().AsNoTracking()
            .Where(t => t.TrackId == trackId && playlistIds.Contains(t.PlaylistId))
            .Select(t => t.PlaylistId)
            .ToListAsync(cancellationToken)).ToHashSet();

    public void Add(Playlist playlist) => db.Set<Playlist>().Add(playlist);

    public void Remove(Playlist playlist) => db.Set<Playlist>().Remove(playlist);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static async Task<Paged<Playlist>> PageAsync(IOrderedQueryable<Playlist> query, PageRequest page, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<Playlist> items = total <= page.Skip ? [] : await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<Playlist>(items, page.Page, page.PageSize, total);
    }
}

/// <summary>Playlists' side of the Moderation contract. Stage* changes are saved by the caller's SaveChanges.</summary>
internal sealed class EfPlaylistModeration(AppDbContext db, TimeProvider time) : IPlaylistModeration
{
    public async Task<PlaylistInfo?> FindAsync(Guid playlistId, CancellationToken cancellationToken) =>
        await db.Set<Playlist>().AsNoTracking().Where(p => p.Id == playlistId)
            .Select(p => new PlaylistInfo(p.Id, p.Title, p.OwnerUserId, p.Visibility == PlaylistVisibility.Public, p.TakenDownAt != null))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> StageTakedownAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await db.Set<Playlist>().FindAsync([playlistId], cancellationToken);
        playlist?.TakeDown(time.GetUtcNow());
        return playlist is not null;
    }

    public async Task<bool> StageRestoreAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        var playlist = await db.Set<Playlist>().FindAsync([playlistId], cancellationToken);
        playlist?.Restore();
        return playlist is not null;
    }
}
