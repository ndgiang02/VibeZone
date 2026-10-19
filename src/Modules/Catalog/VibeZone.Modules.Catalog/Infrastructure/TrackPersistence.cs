using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Infrastructure;

internal sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> b)
    {
        b.ToTable("artists", CatalogModule.Schema);
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Slug).HasMaxLength(Artist.MaxSlugLength).IsRequired();
        b.Property(a => a.Name).HasMaxLength(Artist.MaxNameLength).IsRequired();
        b.Property(a => a.Bio).HasMaxLength(Artist.MaxBioLength);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(a => a.Slug).IsUnique();
        // One artist profile per account. user_id references identity.users; the FK is added in SQL by the
        // migration because the Identity model belongs to another module (ADR-0001: stable-key FKs only).
        b.HasIndex(a => a.UserId).IsUnique();
    }
}

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> b)
    {
        b.ToTable("tracks", CatalogModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_tracks_status", $"status IN ({EnumCheck.Values<TrackStatus>()})");
            t.HasCheckConstraint("ck_tracks_visibility", $"visibility IN ({EnumCheck.Values<TrackVisibility>()})");
            t.HasCheckConstraint("ck_tracks_duration", "duration_ms IS NULL OR duration_ms > 0");
        });
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Title).HasMaxLength(Track.MaxTitleLength).IsRequired();
        b.Property(t => t.Description).HasMaxLength(Track.MaxDescriptionLength);
        b.Property(t => t.Status).HasConversion(new LowerCaseEnumConverter<TrackStatus>()).HasMaxLength(16);
        b.Property(t => t.Visibility).HasConversion(new LowerCaseEnumConverter<TrackVisibility>()).HasMaxLength(16);
        b.Property(t => t.ProcessingError).HasMaxLength(64);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<Artist>().WithMany().HasForeignKey(t => t.ArtistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Genre>().WithMany().HasForeignKey(t => t.GenreId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(t => new { t.ArtistId, t.CreatedAt }).IsDescending(false, true);
        b.HasIndex(t => t.PublishedAt)
            .IsDescending()
            .HasDatabaseName("ix_tracks_new_releases")
            .HasFilter("status = 'ready' AND visibility = 'public' AND deleted_at IS NULL");
    }
}

internal sealed class EfCatalogRepository(AppDbContext db) : ICatalogRepository
{
    public Task<Artist?> FindArtistByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Set<Artist>().SingleOrDefaultAsync(a => a.UserId == userId, cancellationToken);

    public Task<Artist?> FindArtistBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Set<Artist>().AsNoTracking().SingleOrDefaultAsync(a => a.Slug == slug, cancellationToken);

    public Task<bool> ArtistSlugTakenAsync(string slug, CancellationToken cancellationToken) =>
        db.Set<Artist>().AnyAsync(a => a.Slug == slug, cancellationToken);

    public Task<Genre?> FindGenreAsync(string slug, CancellationToken cancellationToken) =>
        db.Set<Genre>().AsNoTracking().SingleOrDefaultAsync(g => g.Slug == slug, cancellationToken);

    public async Task<TrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await Rows(db.Set<Track>().Where(t => t.Id == trackId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TrackRow>> ListArtistTracksAsync(Guid artistId, CancellationToken cancellationToken) =>
        await Rows(db.Set<Track>().AsNoTracking().Where(t => t.ArtistId == artistId && t.DeletedAt == null).OrderByDescending(t => t.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrackRow>> ListNewReleasesAsync(int limit, CancellationToken cancellationToken) =>
        await Rows(db.Set<Track>().AsNoTracking()
                .Where(t => t.Status == TrackStatus.Ready && t.Visibility == TrackVisibility.Public && t.DeletedAt == null)
                .OrderByDescending(t => t.PublishedAt)
                .Take(limit))
            .ToListAsync(cancellationToken);

    public void Add(Artist artist) => db.Set<Artist>().Add(artist);

    public void Add(Track track) => db.Set<Track>().Add(track);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<TrackRow> Rows(IQueryable<Track> tracks) =>
        from t in tracks
        join a in db.Set<Artist>() on t.ArtistId equals a.Id
        join g in db.Set<Genre>() on t.GenreId equals (int?)g.Id into genres
        from g in genres.DefaultIfEmpty()
        select new TrackRow(t, a, g);
}

/// <summary>Catalog's side of the Media contract. Stage* changes are saved by the caller's SaveChanges.</summary>
internal sealed class EfTrackCatalog(AppDbContext db, TimeProvider time) : ITrackCatalog
{
    public async Task<OwnedTrack?> FindOwnedAsync(Guid trackId, Guid userId, CancellationToken cancellationToken)
    {
        var owned = await (from t in db.Set<Track>()
                           join a in db.Set<Artist>() on t.ArtistId equals a.Id
                           where t.Id == trackId && a.UserId == userId && t.DeletedAt == null
                           select new { t.Id, t.Status })
            .SingleOrDefaultAsync(cancellationToken);
        return owned is null ? null : new OwnedTrack(owned.Id, Enum.Parse<TrackLifecycle>(owned.Status.ToString()));
    }

    public async Task<bool> CanStreamAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await (from t in db.Set<Track>().AsNoTracking()
                         join a in db.Set<Artist>() on t.ArtistId equals a.Id
                         where t.Id == trackId
                         select new { Track = t, a.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        return row is not null && row.Track.IsPlayableBy(viewerUserId == row.UserId, viewerIsModerator);
    }

    public async Task StageProcessingAsync(Guid trackId, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).StartProcessing(time.GetUtcNow());

    public async Task StageReadyAsync(Guid trackId, int durationMs, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).MarkReady(durationMs, time.GetUtcNow());

    public async Task StageFailedAsync(Guid trackId, string reasonCode, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).MarkFailed(reasonCode, time.GetUtcNow());

    public async Task StageCancelledAsync(Guid trackId, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).Cancel(time.GetUtcNow());

    private async Task<Track> LoadAsync(Guid trackId, CancellationToken cancellationToken) =>
        await db.Set<Track>().SingleAsync(t => t.Id == trackId, cancellationToken);
}
