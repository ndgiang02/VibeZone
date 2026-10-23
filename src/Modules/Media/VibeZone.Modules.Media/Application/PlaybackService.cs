using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>Who a playback token was issued to; the track's visibility is re-checked on every request.</summary>
public sealed record PlaybackGrant(Guid TrackId, Guid? ViewerUserId, bool ViewerIsModerator);

/// <summary>Opaque, expiring tokens that let a media player fetch a track without credentials (ADR-0008).</summary>
public interface IPlaybackTokens
{
    string Issue(PlaybackGrant grant, DateTimeOffset expiresAt);

    PlaybackGrant? Read(string token, DateTimeOffset now);
}

public sealed record PlaybackTicket(MediaAsset Audio, string Token, DateTimeOffset ExpiresAt);

/// <summary>Authorization happens here, before any byte of audio is read (ADR-0005).</summary>
public sealed class PlaybackService(
    ITrackCatalog catalog,
    IMediaRepository repository,
    IPlaybackTokens tokens,
    IOptions<MediaOptions> options,
    TimeProvider time)
{
    private static readonly DomainError NotFound = DomainError.NotFound("track_not_found", "Track not found.");

    internal static readonly DomainError InvalidToken =
        DomainError.Unauthorized("playback_token_invalid", "The playback link is invalid or expired. Request a new one.");

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

    /// <summary>
    /// Resolves an asset for a request that carries a playback token instead of credentials. The token
    /// only stands in for the viewer's identity: a track made private or deleted stops playing at once.
    /// </summary>
    public async Task<Result<MediaAsset>> ResolveWithTokenAsync(Guid trackId, MediaAssetKind kind, string token, CancellationToken cancellationToken)
    {
        var grant = tokens.Read(token, time.GetUtcNow());
        return grant is null || grant.TrackId != trackId
            ? InvalidToken
            : await ResolveAsync(trackId, kind, grant.ViewerUserId, grant.ViewerIsModerator, cancellationToken);
    }

    /// <summary>Checks the viewer may play the track now and issues a token for its stream URL.</summary>
    public async Task<Result<PlaybackTicket>> CreateTicketAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var audio = await ResolveAsync(trackId, MediaAssetKind.PlaybackAac, viewerUserId, viewerIsModerator, cancellationToken);
        if (!audio.IsSuccess)
        {
            return audio.Error!;
        }

        var expiresAt = time.GetUtcNow() + options.Value.PlaybackTokenLifetime;
        var token = tokens.Issue(new PlaybackGrant(trackId, viewerUserId, viewerIsModerator), expiresAt);
        return new PlaybackTicket(audio.Value, token, expiresAt);
    }
}
