using VibeZone.SharedKernel;

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

/// <summary>Metadata for a track created together with its upload. <paramref name="Visibility"/> is Public/Unlisted/Private.</summary>
public sealed record NewTrackDraft(string? Title, string? Description, string? GenreSlug, string? Visibility);

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

    /// <summary>True if the viewer may see the track (and its artwork) — the same rule as <c>GET /tracks/{id}</c>.</summary>
    Task<bool> CanViewAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken);

    /// <summary>Small cover URL of each given track that has one and isn't deleted (e.g. for playlist mosaics).</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetArtworkUrlsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the metadata and stages a new Draft track owned by the user's artist profile. Fails with
    /// <c>artist_required</c> (forbidden) or a field validation error; nothing is staged on failure.
    /// </summary>
    Task<Result<Guid>> StageNewTrackAsync(Guid userId, NewTrackDraft draft, CancellationToken cancellationToken);

    /// <summary>Points the track at its current artwork (an opaque version id), or clears it.</summary>
    Task StageArtworkAsync(Guid trackId, Guid? artworkId, CancellationToken cancellationToken);

    Task StageProcessingAsync(Guid trackId, CancellationToken cancellationToken);

    Task StageReadyAsync(Guid trackId, int durationMs, CancellationToken cancellationToken);

    Task StageFailedAsync(Guid trackId, string reasonCode, CancellationToken cancellationToken);

    Task StageCancelledAsync(Guid trackId, CancellationToken cancellationToken);
}

/// <summary>Public artwork URLs (served by the Media module). The version makes a changed cover a new URL.</summary>
public static class TrackArtworkPaths
{
    public const string Small = "small";
    public const string Large = "large";

    public static string For(Guid trackId, Guid artworkId, string size) => $"/api/v1/tracks/{trackId}/artwork/{size}?v={artworkId:N}";
}
