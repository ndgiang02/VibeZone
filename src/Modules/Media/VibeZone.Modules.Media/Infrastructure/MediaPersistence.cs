using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Domain;

namespace VibeZone.Modules.Media.Infrastructure;

// track_id columns reference catalog.tracks; the FKs are added in SQL by the migration because the Track
// model belongs to the Catalog module (ADR-0001: cross-module FKs only to stable keys).

internal sealed class UploadJobConfiguration : IEntityTypeConfiguration<UploadJob>
{
    public void Configure(EntityTypeBuilder<UploadJob> b)
    {
        b.ToTable("upload_jobs", MediaModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_upload_jobs_status", $"status IN ({EnumCheck.Values<UploadJobStatus>()})");
            t.HasCheckConstraint("ck_upload_jobs_size", "size_bytes > 0");
        });
        b.HasKey(j => j.Id);
        b.Property(j => j.Id).ValueGeneratedNever();
        b.Property(j => j.Status).HasConversion(new LowerCaseEnumConverter<UploadJobStatus>()).HasMaxLength(16);
        b.Property(j => j.OriginalFileName).HasMaxLength(UploadJob.MaxFileNameLength);
        b.Property(j => j.DeclaredContentType).HasMaxLength(127);
        b.Property(j => j.Sha256).HasMaxLength(64).IsFixedLength();
        b.Property(j => j.ObjectKey).HasMaxLength(512);
        b.Property(j => j.LockedBy).HasMaxLength(128);
        b.Property(j => j.LastError).HasMaxLength(UploadJob.MaxErrorLength);
        b.Property<uint>("xmin").IsRowVersion();
        // Named indexes: several indexes cover track_id, and unnamed HasIndex calls on the same column merge.
        b.HasIndex(j => j.TrackId, "ix_upload_jobs_track_id").HasDatabaseName("ix_upload_jobs_track_id");
        // At most one active job per track.
        b.HasIndex(j => j.TrackId, "ux_upload_jobs_active_track").HasDatabaseName("ux_upload_jobs_active_track").IsUnique().HasFilter("status IN ('uploaded', 'processing')");
        // The queue: due jobs and expired leases.
        b.HasIndex(j => j.NextAttemptAt, "ix_upload_jobs_queue").HasDatabaseName("ix_upload_jobs_queue").HasFilter("status IN ('uploaded', 'processing')");
    }
}

// artist_id references catalog.artists; the FK is added in SQL by the migration (ADR-0001).
internal sealed class ArtistImageConfiguration : IEntityTypeConfiguration<ArtistImage>
{
    public void Configure(EntityTypeBuilder<ArtistImage> b)
    {
        b.ToTable("artist_images", MediaModule.Schema, t =>
            t.HasCheckConstraint("ck_artist_images_kind", $"kind IN ({EnumCheck.Values<ArtistImageKind>()})"));
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Kind).HasConversion(new LowerCaseEnumConverter<ArtistImageKind>()).HasMaxLength(32);
        b.Property(i => i.ObjectKey).HasMaxLength(512);
        b.Property(i => i.ContentType).HasMaxLength(127);
        b.HasIndex(i => new { i.ArtistId, i.Kind }).IsUnique();
        b.HasIndex(i => i.ObjectKey).IsUnique();
    }
}

// user_id references identity.users; the FK is added in SQL by the migration (ADR-0001).
internal sealed class UserImageConfiguration : IEntityTypeConfiguration<UserImage>
{
    public void Configure(EntityTypeBuilder<UserImage> b)
    {
        b.ToTable("user_images", MediaModule.Schema, t =>
            t.HasCheckConstraint("ck_user_images_kind", $"kind IN ({EnumCheck.Values<UserImageKind>()})"));
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Kind).HasConversion(new LowerCaseEnumConverter<UserImageKind>()).HasMaxLength(32);
        b.Property(i => i.ObjectKey).HasMaxLength(512);
        b.Property(i => i.ContentType).HasMaxLength(127);
        b.HasIndex(i => new { i.UserId, i.Kind }).IsUnique();
        b.HasIndex(i => i.ObjectKey).IsUnique();
    }
}

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> b)
    {
        b.ToTable("media_assets", MediaModule.Schema, t =>
            t.HasCheckConstraint("ck_media_assets_kind", $"kind IN ({EnumCheck.Values<MediaAssetKind>()})"));
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Kind).HasConversion(new LowerCaseEnumConverter<MediaAssetKind>()).HasMaxLength(32);
        b.Property(a => a.ObjectKey).HasMaxLength(512);
        b.Property(a => a.ContentType).HasMaxLength(127);
        b.Property(a => a.Sha256).HasMaxLength(64).IsFixedLength();
        b.Property(a => a.Codec).HasMaxLength(32);
        b.HasIndex(a => new { a.TrackId, a.Kind }).IsUnique();
        b.HasIndex(a => a.ObjectKey).IsUnique();
    }
}

internal sealed class EfMediaRepository(AppDbContext db, TimeProvider time) : IMediaRepository
{
    public async Task<UploadJob?> FindJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        // Always the current row: lease heartbeats update it (and its xmin) while a job is being processed.
        if (db.Set<UploadJob>().Local.FirstOrDefault(j => j.Id == jobId) is { } tracked)
        {
            var entry = db.Entry(tracked);
            await entry.ReloadAsync(cancellationToken);
            return entry.State == EntityState.Detached ? null : tracked;
        }

        return await db.Set<UploadJob>().SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }

    public Task<UploadJob?> FindActiveJobAsync(Guid trackId, CancellationToken cancellationToken) =>
        db.Set<UploadJob>().SingleOrDefaultAsync(
            j => j.TrackId == trackId && (j.Status == UploadJobStatus.Uploaded || j.Status == UploadJobStatus.Processing), cancellationToken);

    public Task<UploadJob?> FindLatestJobAsync(Guid trackId, CancellationToken cancellationToken) =>
        db.Set<UploadJob>().AsNoTracking().Where(j => j.TrackId == trackId).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task<Guid?> ClaimNextJobAsync(string workerId, TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var until = now + lease;
        // SKIP LOCKED lets concurrent workers claim different jobs without blocking each other.
        var ids = await db.Database.SqlQuery<Guid>($"""
            UPDATE media.upload_jobs
            SET status = 'processing', locked_by = {workerId}, locked_until = {until},
                attempt_count = attempt_count + 1, updated_at = {now}
            WHERE id = (
                SELECT id FROM media.upload_jobs
                WHERE (status = 'uploaded' AND next_attempt_at <= {now})
                   OR (status = 'processing' AND locked_until < {now})
                ORDER BY next_attempt_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1)
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken);
        return ids.Count == 0 ? null : ids[0];
    }

    public async Task ExtendLeaseAsync(Guid jobId, string workerId, TimeSpan lease, CancellationToken cancellationToken)
    {
        var until = time.GetUtcNow() + lease;
        await db.Set<UploadJob>()
            .Where(j => j.Id == jobId && j.LockedBy == workerId && j.Status == UploadJobStatus.Processing)
            .ExecuteUpdateAsync(u => u.SetProperty(j => j.LockedUntil, until), cancellationToken);
    }

    public async Task<UploadJobStatus?> ReadStatusAsync(Guid jobId, CancellationToken cancellationToken) =>
        await db.Set<UploadJob>().AsNoTracking().Where(j => j.Id == jobId).Select(j => (UploadJobStatus?)j.Status).SingleOrDefaultAsync(cancellationToken);

    public Task<MediaAsset?> FindAssetAsync(Guid trackId, MediaAssetKind kind, CancellationToken cancellationToken) =>
        db.Set<MediaAsset>().AsNoTracking().SingleOrDefaultAsync(a => a.TrackId == trackId && a.Kind == kind, cancellationToken);

    public async Task<IReadOnlyList<MediaAsset>> ListAssetsAsync(Guid trackId, CancellationToken cancellationToken) =>
        await db.Set<MediaAsset>().Where(a => a.TrackId == trackId).ToListAsync(cancellationToken);

    public void Add(UploadJob job) => db.Set<UploadJob>().Add(job);

    public void Add(MediaAsset asset) => db.Set<MediaAsset>().Add(asset);

    public void Remove(MediaAsset asset) => db.Set<MediaAsset>().Remove(asset);

    public async Task<IReadOnlyList<ArtistImage>> ListArtistImagesAsync(Guid artistId, CancellationToken cancellationToken) =>
        await db.Set<ArtistImage>().Where(i => i.ArtistId == artistId).ToListAsync(cancellationToken);

    public Task<ArtistImage?> FindArtistImageAsync(Guid artistId, ArtistImageKind kind, CancellationToken cancellationToken) =>
        db.Set<ArtistImage>().AsNoTracking().SingleOrDefaultAsync(i => i.ArtistId == artistId && i.Kind == kind, cancellationToken);

    public void Add(ArtistImage image) => db.Set<ArtistImage>().Add(image);

    public void Remove(ArtistImage image) => db.Set<ArtistImage>().Remove(image);

    public async Task<IReadOnlyList<UserImage>> ListUserImagesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Set<UserImage>().Where(i => i.UserId == userId).ToListAsync(cancellationToken);

    public Task<UserImage?> FindUserImageAsync(Guid userId, UserImageKind kind, CancellationToken cancellationToken) =>
        db.Set<UserImage>().AsNoTracking().SingleOrDefaultAsync(i => i.UserId == userId && i.Kind == kind, cancellationToken);

    public void Add(UserImage image) => db.Set<UserImage>().Add(image);

    public void Remove(UserImage image) => db.Set<UserImage>().Remove(image);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));

    public Task CommitAsync(IAsyncDisposable transaction, CancellationToken cancellationToken) =>
        ((Transaction)transaction).Inner.CommitAsync(cancellationToken);

    /// <summary>Disposing without commit rolls back.</summary>
    private sealed class Transaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction inner) : IAsyncDisposable
    {
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction Inner => inner;

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
