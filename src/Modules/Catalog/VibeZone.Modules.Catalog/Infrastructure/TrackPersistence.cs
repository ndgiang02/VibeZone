using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Catalog.Domain;
using VibeZone.SharedKernel;

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

internal sealed class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> b)
    {
        b.ToTable("albums", CatalogModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_albums_kind", $"kind IN ({EnumCheck.Values<AlbumKind>()})");
            t.HasCheckConstraint("ck_albums_visibility", $"visibility IN ({EnumCheck.Values<TrackVisibility>()})");
        });
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Title).HasMaxLength(Album.MaxTitleLength).IsRequired();
        b.Property(a => a.Description).HasMaxLength(Album.MaxDescriptionLength);
        b.Property(a => a.Kind).HasConversion(new LowerCaseEnumConverter<AlbumKind>()).HasMaxLength(16);
        b.Property(a => a.Visibility).HasConversion(new LowerCaseEnumConverter<TrackVisibility>()).HasMaxLength(16);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<Artist>().WithMany().HasForeignKey(a => a.ArtistId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(a => new { a.ArtistId, a.CreatedAt }).IsDescending(false, true);
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
        b.Property(t => t.TakedownReason).HasMaxLength(32);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<Artist>().WithMany().HasForeignKey(t => t.ArtistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Genre>().WithMany().HasForeignKey(t => t.GenreId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Album>().WithMany().HasForeignKey(t => t.AlbumId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(t => new { t.AlbumId, t.TrackNumber }).HasFilter("album_id IS NOT NULL");
        b.HasIndex(t => new { t.ArtistId, t.CreatedAt }).IsDescending(false, true);
        b.HasIndex(t => t.PublishedAt)
            .IsDescending()
            .HasDatabaseName("ix_tracks_new_releases")
            .HasFilter("status = 'ready' AND visibility = 'public' AND deleted_at IS NULL AND taken_down_at IS NULL");
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

    public Task<Paged<TrackRow>> PageArtistTracksAsync(Guid artistId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<Track>().AsNoTracking().Where(t => t.ArtistId == artistId && t.DeletedAt == null).OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id), page, cancellationToken);

    public Task<Paged<TrackRow>> PageNewReleasesAsync(PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<Track>().AsNoTracking()
                .Where(t => t.Status == TrackStatus.Ready && t.Visibility == TrackVisibility.Public && t.DeletedAt == null && t.TakenDownAt == null)
                .OrderByDescending(t => t.PublishedAt).ThenByDescending(t => t.Id),
            page,
            cancellationToken);

    public async Task<AlbumRow?> FindAlbumAsync(Guid albumId, CancellationToken cancellationToken) =>
        await (from al in db.Set<Album>()
               join a in db.Set<Artist>() on al.ArtistId equals a.Id
               where al.Id == albumId
               select new AlbumRow(al, a)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Album>> ListArtistAlbumsAsync(Guid artistId, CancellationToken cancellationToken) =>
        await db.Set<Album>().AsNoTracking().Where(a => a.ArtistId == artistId && a.DeletedAt == null)
            .OrderByDescending(a => a.ReleaseDate).ThenByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrackRow>> ListAlbumTracksAsync(IReadOnlyCollection<Guid> albumIds, CancellationToken cancellationToken) =>
        albumIds.Count == 0
            ? []
            : await Rows(db.Set<Track>().AsNoTracking().Where(t => t.AlbumId != null && albumIds.Contains(t.AlbumId.Value) && t.DeletedAt == null)
                    .OrderBy(t => t.AlbumId).ThenBy(t => t.TrackNumber))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Track>> ListArtistTrackEntitiesAsync(Guid artistId, CancellationToken cancellationToken) =>
        await db.Set<Track>().Where(t => t.ArtistId == artistId && t.DeletedAt == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrackRow>> ListTracksAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        trackIds.Count == 0 ? [] : await Rows(db.Set<Track>().AsNoTracking().Where(t => trackIds.Contains(t.Id))).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Artist>> ListArtistsAsync(IReadOnlyCollection<Guid> artistIds, CancellationToken cancellationToken) =>
        artistIds.Count == 0 ? [] : await db.Set<Artist>().AsNoTracking().Where(a => artistIds.Contains(a.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AlbumRow>> ListAlbumRowsAsync(IReadOnlyCollection<Guid> albumIds, CancellationToken cancellationToken) =>
        albumIds.Count == 0
            ? []
            : await (from al in db.Set<Album>().AsNoTracking()
                     join a in db.Set<Artist>() on al.ArtistId equals a.Id
                     where albumIds.Contains(al.Id)
                     select new AlbumRow(al, a)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrackRow>> ListRelatedAsync(Guid trackId, Guid artistId, int? genreId, int limit, CancellationToken cancellationToken) =>
        await Rows(db.Set<Track>().AsNoTracking()
                .Where(t => t.Id != trackId && t.Status == TrackStatus.Ready && t.Visibility == TrackVisibility.Public && t.DeletedAt == null && t.TakenDownAt == null)
                .Where(t => t.ArtistId == artistId || (genreId != null && t.GenreId == genreId))
                .OrderBy(t => t.ArtistId == artistId ? 0 : 1).ThenByDescending(t => t.PublishedAt).ThenBy(t => t.Id)
                .Take(limit))
            .ToListAsync(cancellationToken);

    public Task<Paged<TrackRow>> PageAllAsync(string? query, TrackStatus? status, bool? takenDown, PageRequest page, CancellationToken cancellationToken)
    {
        var tracks = db.Set<Track>().AsNoTracking().Where(t => t.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = "%" + query.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            tracks = tracks.Where(t => EF.Functions.ILike(t.Title, pattern));
        }

        if (status is { } s)
        {
            tracks = tracks.Where(t => t.Status == s);
        }

        if (takenDown is { } down)
        {
            tracks = down ? tracks.Where(t => t.TakenDownAt != null) : tracks.Where(t => t.TakenDownAt == null);
        }

        return PageAsync(tracks.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id), page, cancellationToken);
    }

    public void Add(Artist artist) => db.Set<Artist>().Add(artist);

    public void Add(Track track) => db.Set<Track>().Add(track);

    public void Add(Album album) => db.Set<Album>().Add(album);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private async Task<Paged<TrackRow>> PageAsync(IOrderedQueryable<Track> tracks, PageRequest page, CancellationToken cancellationToken)
    {
        var total = await tracks.CountAsync(cancellationToken);
        IReadOnlyList<TrackRow> items = total <= page.Skip ? [] : await Rows(tracks.Skip(page.Skip).Take(page.PageSize)).ToListAsync(cancellationToken);
        return new Paged<TrackRow>(items, page.Page, page.PageSize, total);
    }

    private IQueryable<TrackRow> Rows(IQueryable<Track> tracks) =>
        from t in tracks
        join a in db.Set<Artist>() on t.ArtistId equals a.Id
        join g in db.Set<Genre>() on t.GenreId equals (int?)g.Id into genres
        from g in genres.DefaultIfEmpty()
        join al in db.Set<Album>() on t.AlbumId equals (Guid?)al.Id into albums
        from al in albums.DefaultIfEmpty()
        select new TrackRow(t, a, g, al);
}

/// <summary>Catalog's side of the Media contract. Stage* changes are saved by the caller's SaveChanges.</summary>
internal sealed class EfTrackCatalog(AppDbContext db, TrackService tracks, IOutbox outbox, TimeProvider time) : ITrackCatalog
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

    public async Task<bool> CanViewAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await (from t in db.Set<Track>().AsNoTracking()
                         join a in db.Set<Artist>() on t.ArtistId equals a.Id
                         where t.Id == trackId
                         select new { Track = t, a.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        return row is not null && row.Track.IsVisibleTo(viewerUserId == row.UserId, viewerIsModerator);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetArtworkUrlsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await db.Set<Track>().AsNoTracking()
            .Where(t => trackIds.Contains(t.Id) && t.ArtworkId != null && t.DeletedAt == null)
            .Select(t => new { t.Id, t.ArtworkId })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => TrackArtworkPaths.For(r.Id, r.ArtworkId!.Value, TrackArtworkPaths.Small));
    }

    public async Task<Result<Guid>> StageNewTrackAsync(Guid userId, NewTrackDraft draft, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TrackVisibility>(draft.Visibility, ignoreCase: true, out var visibility) || !Enum.IsDefined(visibility))
        {
            return DomainError.Validation("track_visibility_invalid", "Unknown visibility.", "visibility");
        }

        var staged = await tracks.StageCreateAsync(userId, new SaveTrackRequest(draft.Title, draft.Description, draft.GenreSlug, visibility), cancellationToken);
        return staged.IsSuccess ? staged.Value.Track.Id : staged.Error!;
    }

    public async Task StageArtworkAsync(Guid trackId, Guid? artworkId, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).SetArtwork(artworkId, time.GetUtcNow());

    public async Task StageProcessingAsync(Guid trackId, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).StartProcessing(time.GetUtcNow());

    public async Task StageReadyAsync(Guid trackId, int durationMs, CancellationToken cancellationToken)
    {
        var track = await LoadAsync(trackId, cancellationToken);
        track.MarkReady(durationMs, time.GetUtcNow());
        await StageProcessedEventAsync(track, null, cancellationToken);
    }

    public async Task StageFailedAsync(Guid trackId, string reasonCode, CancellationToken cancellationToken)
    {
        var track = await LoadAsync(trackId, cancellationToken);
        track.MarkFailed(reasonCode, time.GetUtcNow());
        await StageProcessedEventAsync(track, reasonCode, cancellationToken);
    }

    public async Task StageCancelledAsync(Guid trackId, CancellationToken cancellationToken) =>
        (await LoadAsync(trackId, cancellationToken)).Cancel(time.GetUtcNow());

    public async Task<IReadOnlyDictionary<Guid, TrackInfo>> GetInfoAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken)
    {
        if (trackIds.Count == 0)
        {
            return new Dictionary<Guid, TrackInfo>();
        }

        var rows = await (from t in db.Set<Track>().AsNoTracking()
                          join a in db.Set<Artist>() on t.ArtistId equals a.Id
                          where trackIds.Contains(t.Id) && t.DeletedAt == null
                          select new { Track = t, Artist = a })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(
            r => r.Track.Id,
            r => new TrackInfo(r.Track.Id, r.Track.Title, r.Artist.Id, r.Artist.Name, r.Artist.Slug, r.Artist.UserId,
                r.Track.ToArtwork()?.Small, r.Track.DurationMs, r.Track.IsTakenDown));
    }

    public async Task<bool> StageTakedownAsync(Guid trackId, string reasonCode, CancellationToken cancellationToken)
    {
        var track = await db.Set<Track>().FindAsync([trackId], cancellationToken);
        if (track is null || track.DeletedAt is not null)
        {
            return false;
        }

        track.TakeDown(reasonCode, time.GetUtcNow());
        return true;
    }

    public async Task<bool> StageRestoreAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await db.Set<Track>().FindAsync([trackId], cancellationToken);
        if (track is null || track.DeletedAt is not null)
        {
            return false;
        }

        track.Restore(time.GetUtcNow());
        return true;
    }

    private async Task StageProcessedEventAsync(Track track, string? errorCode, CancellationToken cancellationToken)
    {
        var ownerUserId = await db.Set<Artist>().Where(a => a.Id == track.ArtistId).Select(a => a.UserId).SingleAsync(cancellationToken);
        outbox.Enqueue(new TrackProcessed(track.Id, ownerUserId, track.Title, errorCode is null, errorCode));
    }

    // FindAsync also returns a track staged (Added) earlier in the same unit of work.
    private async Task<Track> LoadAsync(Guid trackId, CancellationToken cancellationToken) =>
        await db.Set<Track>().FindAsync([trackId], cancellationToken) ?? throw new InvalidOperationException($"Track {trackId} not found.");
}

/// <summary>Catalog's side of the Media contract for artist images.</summary>
internal sealed class EfArtistCatalog(AppDbContext db, TimeProvider time) : IArtistCatalog
{
    public async Task<Guid?> FindArtistIdForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Set<Artist>().Where(a => a.UserId == userId).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid artistId, CancellationToken cancellationToken) =>
        db.Set<Artist>().AnyAsync(a => a.Id == artistId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ArtistSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> artistIds, CancellationToken cancellationToken)
    {
        if (artistIds.Count == 0)
        {
            return new Dictionary<Guid, ArtistSummary>();
        }

        var artists = await db.Set<Artist>().AsNoTracking().Where(a => artistIds.Contains(a.Id)).ToListAsync(cancellationToken);
        return artists.ToDictionary(a => a.Id, a => new ArtistSummary(a.Id, a.Slug, a.Name, a.ToRef().AvatarUrl, a.UserId));
    }

    public async Task StageImageAsync(Guid artistId, ArtistImageSlot slot, Guid? imageId, CancellationToken cancellationToken)
    {
        var artist = await db.Set<Artist>().FindAsync([artistId], cancellationToken) ?? throw new InvalidOperationException($"Artist {artistId} not found.");
        if (slot == ArtistImageSlot.Avatar)
        {
            artist.SetAvatar(imageId, time.GetUtcNow());
        }
        else
        {
            artist.SetBanner(imageId, time.GetUtcNow());
        }
    }
}
