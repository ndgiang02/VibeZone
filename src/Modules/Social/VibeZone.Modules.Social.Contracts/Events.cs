using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Contracts;

public sealed record TrackLiked(Guid TrackId, Guid UserId, Guid TrackOwnerUserId, string TrackTitle, DateTimeOffset LikedAt) : IIntegrationEvent
{
    public static string EventType => "social.track_liked";

    public static int EventVersion => 1;
}

public sealed record ArtistFollowed(Guid ArtistId, string ArtistSlug, Guid ArtistOwnerUserId, Guid FollowerUserId, DateTimeOffset FollowedAt) : IIntegrationEvent
{
    public static string EventType => "social.artist_followed";

    public static int EventVersion => 1;
}

/// <param name="ParentAuthorUserId">Author of the comment this one replies to, if it's a reply.</param>
/// <param name="Excerpt">The first characters of the body (plain text) for notifications.</param>
public sealed record CommentPosted(
    Guid CommentId,
    Guid TrackId,
    string TrackTitle,
    Guid TrackOwnerUserId,
    Guid AuthorUserId,
    Guid? ParentAuthorUserId,
    string Excerpt,
    DateTimeOffset PostedAt) : IIntegrationEvent
{
    public static string EventType => "social.comment_posted";

    public static int EventVersion => 1;
}
