using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Social.Contracts;
using VibeZone.Modules.Social.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Application;

public sealed class LikeService(ISocialRepository repository, ITrackCatalog tracks, IOutbox outbox, TimeProvider time)
{
    /// <summary>Upper bound for <see cref="ListLikedIdsAsync"/> (the web app marks hearts from this set).</summary>
    public const int MaxLikedIds = 5000;

    private static readonly DomainError TrackNotFound = DomainError.NotFound("track_not_found", "Track not found.");

    /// <summary>Likes a track the user can play. Idempotent; notifies the artist the first time.</summary>
    public async Task<Result<TrackEngagementDto>> LikeAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        if (!await tracks.CanStreamAsync(trackId, userId, false, cancellationToken) ||
            (await tracks.GetInfoAsync([trackId], cancellationToken)).GetValueOrDefault(trackId) is not { } info)
        {
            return TrackNotFound;
        }

        if (await repository.FindLikeAsync(userId, trackId, cancellationToken) is null)
        {
            var now = time.GetUtcNow();
            repository.Add(new TrackLike(userId, trackId, now));
            outbox.Enqueue(new TrackLiked(trackId, userId, info.OwnerUserId, info.Title, now));
            await repository.SaveChangesAsync(cancellationToken);
        }

        return await EngagementAsync(trackId, userId, cancellationToken);
    }

    /// <summary>Removes the like if any (also for tracks the user can no longer see).</summary>
    public async Task<TrackEngagementDto> UnlikeAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await repository.FindLikeAsync(userId, trackId, cancellationToken) is { } like)
        {
            repository.Remove(like);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return await EngagementAsync(trackId, userId, cancellationToken);
    }

    public async Task<Result<TrackEngagementDto>> GetEngagementAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken) =>
        await tracks.CanViewAsync(trackId, viewerUserId, viewerIsModerator, cancellationToken)
            ? await EngagementAsync(trackId, viewerUserId, cancellationToken)
            : TrackNotFound;

    public Task<IReadOnlyList<Guid>> ListLikedIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.ListLikedTrackIdsAsync(userId, MaxLikedIds, cancellationToken);

    private async Task<TrackEngagementDto> EngagementAsync(Guid trackId, Guid? viewerUserId, CancellationToken cancellationToken) => new(
        await repository.CountLikesAsync(trackId, cancellationToken),
        await repository.CountCommentsAsync(trackId, cancellationToken),
        viewerUserId is { } userId && await repository.FindLikeAsync(userId, trackId, cancellationToken) is not null);
}

public sealed class FollowService(ISocialRepository repository, IArtistCatalog artists, IOutbox outbox, TimeProvider time)
{
    private static readonly DomainError ArtistNotFound = DomainError.NotFound("artist_not_found", "Artist not found.");

    public async Task<Result<FollowSummaryDto>> FollowAsync(Guid userId, Guid artistId, CancellationToken cancellationToken)
    {
        if ((await artists.GetSummariesAsync([artistId], cancellationToken)).GetValueOrDefault(artistId) is not { } artist)
        {
            return ArtistNotFound;
        }

        if (artist.OwnerUserId == userId)
        {
            return DomainError.Validation("follow_self", "You can't follow your own artist profile.");
        }

        if (await repository.FindFollowAsync(userId, artistId, cancellationToken) is null)
        {
            var now = time.GetUtcNow();
            repository.Add(new ArtistFollow(userId, artistId, now));
            outbox.Enqueue(new ArtistFollowed(artistId, artist.Slug, artist.OwnerUserId, userId, now));
            await repository.SaveChangesAsync(cancellationToken);
        }

        return await SummaryAsync(artistId, userId, cancellationToken);
    }

    public async Task<FollowSummaryDto> UnfollowAsync(Guid userId, Guid artistId, CancellationToken cancellationToken)
    {
        if (await repository.FindFollowAsync(userId, artistId, cancellationToken) is { } follow)
        {
            repository.Remove(follow);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return await SummaryAsync(artistId, userId, cancellationToken);
    }

    public async Task<Result<FollowSummaryDto>> GetSummaryAsync(Guid artistId, Guid? viewerUserId, CancellationToken cancellationToken) =>
        await artists.ExistsAsync(artistId, cancellationToken)
            ? await SummaryAsync(artistId, viewerUserId, cancellationToken)
            : ArtistNotFound;

    public async Task<Paged<FollowedArtistDto>> ListFollowingAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var follows = await repository.PageFollowingAsync(userId, page, cancellationToken);
        var summaries = await artists.GetSummariesAsync(follows.Items.Select(f => f.ArtistId).ToList(), cancellationToken);
        var items = follows.Items.Where(f => summaries.ContainsKey(f.ArtistId))
            .Select(f =>
            {
                var a = summaries[f.ArtistId];
                return new FollowedArtistDto(a.ArtistId, a.Slug, a.Name, a.AvatarUrl, f.CreatedAt);
            })
            .ToList();
        return new Paged<FollowedArtistDto>(items, follows.Page, follows.PageSize, follows.Total);
    }

    private async Task<FollowSummaryDto> SummaryAsync(Guid artistId, Guid? viewerUserId, CancellationToken cancellationToken) => new(
        await repository.CountFollowersAsync(artistId, cancellationToken),
        viewerUserId is { } userId && await repository.FindFollowAsync(userId, artistId, cancellationToken) is not null);
}
