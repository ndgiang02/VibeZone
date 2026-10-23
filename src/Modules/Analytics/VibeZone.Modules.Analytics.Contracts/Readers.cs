using VibeZone.SharedKernel;

namespace VibeZone.Modules.Analytics.Contracts;

public sealed record PlayedTrack(Guid TrackId, DateTimeOffset PlayedAt);

/// <summary>A user's listening history for the Catalog module (which renders track DTOs).</summary>
public interface IListeningHistory
{
    /// <summary>Distinct tracks, most recently started first. Includes tracks the user may no longer see.</summary>
    Task<Paged<PlayedTrack>> PageRecentAsync(Guid userId, PageRequest page, CancellationToken cancellationToken);
}

/// <summary>Trending from daily stats (valid listens and likes over the last days, newer days weigh more).</summary>
public interface ITrendingTracks
{
    /// <summary>Best first, at most <paramref name="limit"/>. Includes tracks that may no longer be listed; the caller filters.</summary>
    Task<IReadOnlyList<Guid>> TopAsync(int limit, CancellationToken cancellationToken);
}
