using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Contracts;

public sealed record LikedTrack(Guid TrackId, DateTimeOffset LikedAt);

/// <summary>A user's liked tracks for the Catalog module (which renders track DTOs).</summary>
public interface ILikedTracks
{
    /// <summary>Newest like first. Includes tracks the user may no longer see; the caller filters.</summary>
    Task<Paged<LikedTrack>> PageAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);
}

public sealed record CommentInfo(Guid CommentId, Guid TrackId, Guid AuthorUserId, string Body, bool Removed);

/// <summary>Reports and removals for the Moderation module. <c>Stage*</c> changes are committed by the caller.</summary>
public interface ICommentModeration
{
    Task<CommentInfo?> FindAsync(Guid commentId, CancellationToken cancellationToken);

    /// <summary>Removes the comment (and hides its replies). False if it doesn't exist.</summary>
    Task<bool> StageRemoveAsync(Guid commentId, CancellationToken cancellationToken);
}
