using VibeZone.Modules.Analytics.Contracts;
using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Social.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

/// <param name="PlayedAt">When the user last started this track.</param>
public sealed record HistoryEntryDto(DateTimeOffset PlayedAt, TrackDto Track);

/// <param name="LikedAt">When the user liked it.</param>
public sealed record LikedTrackDto(DateTimeOffset LikedAt, TrackDto Track);

/// <summary>
/// Track lists whose membership or order another module owns (likes: Social; history, trending: Analytics).
/// Catalog renders them because it owns the track DTO; tracks the viewer can't see any more are left out.
/// </summary>
public sealed class TrackFeedService(ICatalogRepository repository, ILikedTracks likes, IListeningHistory history, ITrendingTracks trending)
{
    public const int RelatedLimit = 12;
    private const int TrendingPool = 100;

    /// <summary>Same artist or same genre (labelled as such in the UI — not personalised).</summary>
    public async Task<Result<IReadOnlyList<TrackDto>>> ListRelatedAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var row = await repository.FindTrackAsync(trackId, cancellationToken);
        if (row is null || !row.Track.IsVisibleTo(viewerUserId == row.Artist.UserId, viewerIsModerator))
        {
            return TrackService.NotFound;
        }

        var related = await repository.ListRelatedAsync(trackId, row.Track.ArtistId, row.Track.GenreId, RelatedLimit, cancellationToken);
        return related.Select(r => r.ToDto(viewerUserId == r.Artist.UserId)).ToList();
    }

    /// <summary>Trending (last 7 days of valid listens and likes), listed tracks only, paged in memory over the top 100.</summary>
    public async Task<Paged<TrackDto>> ListTrendingAsync(PageRequest page, Guid? viewerUserId, CancellationToken cancellationToken)
    {
        var ids = await trending.TopAsync(TrendingPool, cancellationToken);
        var rows = (await repository.ListTracksAsync(ids, cancellationToken)).ToDictionary(r => r.Track.Id);
        var listed = ids.Where(rows.ContainsKey).Select(id => rows[id]).Where(r => r.Track.IsListed).ToList();
        var items = listed.Skip(page.Skip).Take(page.PageSize).Select(r => r.ToDto(viewerUserId == r.Artist.UserId)).ToList();
        return new Paged<TrackDto>(items, page.Page, page.PageSize, listed.Count);
    }

    /// <summary>The user's liked tracks, newest like first. <c>total</c> counts every like, including hidden tracks.</summary>
    public async Task<Paged<LikedTrackDto>> ListLikedAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var liked = await likes.PageAsync(userId, page, cancellationToken);
        var rows = await VisibleRowsAsync(liked.Items.Select(l => l.TrackId).ToList(), userId, cancellationToken);
        var items = liked.Items.Where(l => rows.ContainsKey(l.TrackId))
            .Select(l => new LikedTrackDto(l.LikedAt, rows[l.TrackId].ToDto(rows[l.TrackId].Artist.UserId == userId)))
            .ToList();
        return new Paged<LikedTrackDto>(items, liked.Page, liked.PageSize, liked.Total);
    }

    /// <summary>Recently played, one entry per track, most recent first.</summary>
    public async Task<Paged<HistoryEntryDto>> ListHistoryAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        var played = await history.PageRecentAsync(userId, page, cancellationToken);
        var rows = await VisibleRowsAsync(played.Items.Select(p => p.TrackId).ToList(), userId, cancellationToken);
        var items = played.Items.Where(p => rows.ContainsKey(p.TrackId))
            .Select(p => new HistoryEntryDto(p.PlayedAt, rows[p.TrackId].ToDto(rows[p.TrackId].Artist.UserId == userId)))
            .ToList();
        return new Paged<HistoryEntryDto>(items, played.Page, played.PageSize, played.Total);
    }

    /// <summary>Staff view of every track (moderators: takedown queue, admins: overview).</summary>
    public async Task<Paged<TrackDto>> ListAllAsync(string? query, TrackStatus? status, bool? takenDown, PageRequest page, CancellationToken cancellationToken) =>
        (await repository.PageAllAsync(query, status, takenDown, page, cancellationToken)).Map(r => r.ToDto(isOwner: false, viewerIsModerator: true));

    private async Task<Dictionary<Guid, TrackRow>> VisibleRowsAsync(IReadOnlyCollection<Guid> ids, Guid viewerUserId, CancellationToken cancellationToken) =>
        (await repository.ListTracksAsync(ids, cancellationToken))
            .Where(r => r.Track.IsVisibleTo(r.Artist.UserId == viewerUserId, false))
            .ToDictionary(r => r.Track.Id);
}
