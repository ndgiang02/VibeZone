using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>Authorization happens here, before any byte of audio is read (ADR-0005).</summary>
public sealed class PlaybackService(ITrackCatalog catalog, IMediaRepository repository)
{
    private static readonly DomainError NotFound = DomainError.NotFound("track_not_found", "Track not found.");

    public async Task<Result<MediaAsset>> ResolveAsync(Guid trackId, MediaAssetKind kind, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        if (!await catalog.CanStreamAsync(trackId, viewerUserId, viewerIsModerator, cancellationToken))
        {
            return NotFound;
        }

        return await repository.FindAssetAsync(trackId, kind, cancellationToken) is { } asset
            ? asset
            : DomainError.NotFound("media_not_ready", "Audio isn't available for this track.");
    }
}
