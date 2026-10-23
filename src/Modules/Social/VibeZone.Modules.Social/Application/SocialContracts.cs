using VibeZone.Modules.Social.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Application;

public sealed record TrackEngagementDto(int LikeCount, int CommentCount, bool Liked);

public sealed record FollowSummaryDto(int FollowerCount, bool Following);

public sealed record FollowedArtistDto(Guid Id, string Slug, string Name, string? AvatarUrl, DateTimeOffset FollowedAt);

public sealed record CommentAuthorDto(Guid Id, string DisplayName, string Handle, string? AvatarUrl);

/// <param name="TimestampMs">Moment of the track the comment refers to (top-level comments only), or null.</param>
/// <param name="CanDelete">The viewer may delete it: its author, the track's owner or a moderator.</param>
/// <param name="Replies">Top-level comments only: their replies, oldest first. Empty for replies.</param>
public sealed record CommentDto(
    Guid Id,
    Guid TrackId,
    Guid? ParentId,
    string Body,
    int? TimestampMs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    CommentAuthorDto Author,
    bool IsAuthor,
    bool CanDelete,
    IReadOnlyList<CommentDto> Replies);

/// <param name="ParentId">Reply to this comment (a reply to a reply attaches to its top-level comment).</param>
/// <param name="TimestampMs">Optional moment of the track in ms (ignored for replies).</param>
public sealed record PostCommentRequest(string? Body, int? TimestampMs, Guid? ParentId);

public sealed record EditCommentRequest(string? Body);

/// <summary>Persistence port for the Social module.</summary>
public interface ISocialRepository
{
    Task<TrackLike?> FindLikeAsync(Guid userId, Guid trackId, CancellationToken cancellationToken);

    Task<int> CountLikesAsync(Guid trackId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListLikedTrackIdsAsync(Guid userId, int limit, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<Paged<TrackLike>> PageLikesAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);

    Task<ArtistFollow?> FindFollowAsync(Guid followerUserId, Guid artistId, CancellationToken cancellationToken);

    Task<int> CountFollowersAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<Paged<ArtistFollow>> PageFollowingAsync(Guid followerUserId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked for update; includes deleted ones.</summary>
    Task<Comment?> FindCommentAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>Visible comments (and replies of visible top-level comments).</summary>
    Task<int> CountCommentsAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Not-deleted top-level comments, newest first.</summary>
    Task<Paged<Comment>> PageTopLevelCommentsAsync(Guid trackId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Not-deleted replies to the given comments, oldest first.</summary>
    Task<IReadOnlyList<Comment>> ListRepliesAsync(IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken);

    void Add(TrackLike trackLike);

    void Remove(TrackLike trackLike);

    void Add(ArtistFollow follow);

    void Remove(ArtistFollow follow);

    void Add(Comment comment);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
