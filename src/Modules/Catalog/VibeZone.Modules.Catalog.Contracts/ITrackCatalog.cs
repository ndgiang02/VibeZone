namespace VibeZone.Modules.Catalog.Contracts;

public enum TrackLifecycle
{
    Draft,
    Processing,
    Ready,
    Failed,
    Cancelled,
}

public sealed record OwnedTrack(Guid TrackId, TrackLifecycle Status);

/// <summary>
/// Track state for the Media module. The <c>Stage*</c> methods only record changes in the shared unit of
/// work; the caller commits them together with its own changes (one transaction, ADR-0001).
/// </summary>
public interface ITrackCatalog
{
    /// <summary>The track if <paramref name="userId"/> owns it (via their artist profile) and it isn't deleted.</summary>
    Task<OwnedTrack?> FindOwnedAsync(Guid trackId, Guid userId, CancellationToken cancellationToken);

    /// <summary>True if the viewer may play the track: Ready, not deleted, and public/unlisted or owned/moderator.</summary>
    Task<bool> CanStreamAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken);

    Task StageProcessingAsync(Guid trackId, CancellationToken cancellationToken);

    Task StageReadyAsync(Guid trackId, int durationMs, CancellationToken cancellationToken);

    Task StageFailedAsync(Guid trackId, string reasonCode, CancellationToken cancellationToken);

    Task StageCancelledAsync(Guid trackId, CancellationToken cancellationToken);
}
