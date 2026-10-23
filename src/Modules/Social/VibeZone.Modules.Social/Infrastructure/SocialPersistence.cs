using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Social.Application;
using VibeZone.Modules.Social.Contracts;
using VibeZone.Modules.Social.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Infrastructure;

// user ids reference identity.users, track ids catalog.tracks and artist ids catalog.artists; those FKs are
// added in SQL by the migration because the referenced models belong to other modules (ADR-0001).

internal sealed class TrackLikeConfiguration : IEntityTypeConfiguration<TrackLike>
{
    public void Configure(EntityTypeBuilder<TrackLike> b)
    {
        b.ToTable("likes", SocialModule.Schema);
        b.HasKey(l => new { l.UserId, l.TrackId });
        b.HasIndex(l => new { l.TrackId, l.CreatedAt }).IsDescending(false, true);
        b.HasIndex(l => new { l.UserId, l.CreatedAt }).IsDescending(false, true);
    }
}

internal sealed class ArtistFollowConfiguration : IEntityTypeConfiguration<ArtistFollow>
{
    public void Configure(EntityTypeBuilder<ArtistFollow> b)
    {
        b.ToTable("follows", SocialModule.Schema);
        b.HasKey(f => new { f.FollowerUserId, f.ArtistId });
        b.HasIndex(f => f.ArtistId);
        b.HasIndex(f => new { f.FollowerUserId, f.CreatedAt }).IsDescending(false, true);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments", SocialModule.Schema, t =>
        {
            t.HasCheckConstraint("ck_comments_timestamp", "timestamp_ms IS NULL OR timestamp_ms >= 0");
            t.HasCheckConstraint("ck_comments_body", $"char_length(body) BETWEEN 1 AND {Comment.MaxBodyLength}");
        });
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Body).HasMaxLength(Comment.MaxBodyLength).IsRequired();
        b.Ignore(c => c.IsDeleted);
        b.Ignore(c => c.Excerpt);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<Comment>().WithMany().HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.TrackId, c.CreatedAt }).IsDescending(false, true);
        b.HasIndex(c => new { c.TrackId, c.TimestampMs });
        b.HasIndex(c => c.ParentId).HasFilter("parent_id IS NOT NULL");
    }
}

internal sealed class EfSocialRepository(AppDbContext db) : ISocialRepository
{
    public Task<TrackLike?> FindLikeAsync(Guid userId, Guid trackId, CancellationToken cancellationToken) =>
        db.Set<TrackLike>().SingleOrDefaultAsync(l => l.UserId == userId && l.TrackId == trackId, cancellationToken);

    public Task<int> CountLikesAsync(Guid trackId, CancellationToken cancellationToken) =>
        db.Set<TrackLike>().CountAsync(l => l.TrackId == trackId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListLikedTrackIdsAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await db.Set<TrackLike>().AsNoTracking().Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt).Select(l => l.TrackId).Take(limit).ToListAsync(cancellationToken);

    public Task<Paged<TrackLike>> PageLikesAsync(Guid userId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<TrackLike>().AsNoTracking().Where(l => l.UserId == userId).OrderByDescending(l => l.CreatedAt).ThenBy(l => l.TrackId), page, cancellationToken);

    public Task<ArtistFollow?> FindFollowAsync(Guid followerUserId, Guid artistId, CancellationToken cancellationToken) =>
        db.Set<ArtistFollow>().SingleOrDefaultAsync(f => f.FollowerUserId == followerUserId && f.ArtistId == artistId, cancellationToken);

    public Task<int> CountFollowersAsync(Guid artistId, CancellationToken cancellationToken) =>
        db.Set<ArtistFollow>().CountAsync(f => f.ArtistId == artistId, cancellationToken);

    public Task<Paged<ArtistFollow>> PageFollowingAsync(Guid followerUserId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<ArtistFollow>().AsNoTracking().Where(f => f.FollowerUserId == followerUserId).OrderByDescending(f => f.CreatedAt).ThenBy(f => f.ArtistId), page, cancellationToken);

    public Task<Comment?> FindCommentAsync(Guid commentId, CancellationToken cancellationToken) =>
        db.Set<Comment>().SingleOrDefaultAsync(c => c.Id == commentId, cancellationToken);

    public Task<int> CountCommentsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var comments = db.Set<Comment>();
        return comments.CountAsync(
            c => c.TrackId == trackId && c.DeletedAt == null &&
                 (c.ParentId == null || comments.Any(p => p.Id == c.ParentId && p.DeletedAt == null)),
            cancellationToken);
    }

    public Task<Paged<Comment>> PageTopLevelCommentsAsync(Guid trackId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(db.Set<Comment>().AsNoTracking().Where(c => c.TrackId == trackId && c.ParentId == null && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id), page, cancellationToken);

    public async Task<IReadOnlyList<Comment>> ListRepliesAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken) =>
        parentIds.Count == 0
            ? []
            : await db.Set<Comment>().AsNoTracking().Where(c => c.ParentId != null && parentIds.Contains(c.ParentId.Value) && c.DeletedAt == null)
                .OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);

    public void Add(TrackLike trackLike) => db.Set<TrackLike>().Add(trackLike);

    public void Remove(TrackLike trackLike) => db.Set<TrackLike>().Remove(trackLike);

    public void Add(ArtistFollow follow) => db.Set<ArtistFollow>().Add(follow);

    public void Remove(ArtistFollow follow) => db.Set<ArtistFollow>().Remove(follow);

    public void Add(Comment comment) => db.Set<Comment>().Add(comment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static async Task<Paged<T>> PageAsync<T>(IOrderedQueryable<T> query, PageRequest page, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        IReadOnlyList<T> items = total <= page.Skip ? [] : await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new Paged<T>(items, page.Page, page.PageSize, total);
    }
}

internal sealed class EfLikedTracks(ISocialRepository repository) : ILikedTracks
{
    public async Task<Paged<LikedTrack>> PageAsync(Guid userId, PageRequest page, CancellationToken cancellationToken) =>
        (await repository.PageLikesAsync(userId, page, cancellationToken)).Map(l => new LikedTrack(l.TrackId, l.CreatedAt));
}

/// <summary>Social's side of the Moderation contract. Stage* changes are saved by the caller's SaveChanges.</summary>
internal sealed class EfCommentModeration(AppDbContext db, TimeProvider time) : ICommentModeration
{
    public async Task<CommentInfo?> FindAsync(Guid commentId, CancellationToken cancellationToken) =>
        await db.Set<Comment>().AsNoTracking().Where(c => c.Id == commentId)
            .Select(c => new CommentInfo(c.Id, c.TrackId, c.AuthorUserId, c.Body, c.DeletedAt != null))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> StageRemoveAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await db.Set<Comment>().FindAsync([commentId], cancellationToken);
        comment?.Delete(time.GetUtcNow(), byModeration: true);
        return comment is not null;
    }
}
