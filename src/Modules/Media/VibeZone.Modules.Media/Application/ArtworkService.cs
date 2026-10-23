using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>Cover URLs returned after a change (the same shape the track DTO carries).</summary>
public sealed record TrackArtworkDto(string Small, string Large);

/// <summary>Objects written for a staged cover: delete <see cref="NewKeys"/> if the commit fails, <see cref="ReplacedKeys"/> after it succeeds.</summary>
public sealed record StagedArtwork(Guid ArtworkId, IReadOnlyList<string> NewKeys, IReadOnlyList<string> ReplacedKeys);

/// <summary>
/// Track cover images: the API decodes the untrusted upload, re-encodes it into two square JPEG renditions
/// (dropping EXIF/GPS and any other metadata), stores them privately and points the track at the new version.
/// </summary>
public sealed partial class ArtworkService(
    ITrackCatalog catalog,
    IMediaRepository repository,
    IObjectStorage storage,
    IArtworkProcessor processor,
    IOptions<MediaOptions> options,
    TimeProvider time,
    ILogger<ArtworkService> logger)
{
    private static readonly DomainError TrackNotFound = DomainError.NotFound("track_not_found", "Track not found.");
    private static readonly DomainError ArtworkNotFound = DomainError.NotFound("artwork_not_found", "This track has no cover image.");

    public static DomainError TooLarge(long maxBytes) =>
        DomainError.Validation("artwork_too_large", $"Cover images can be at most {maxBytes / (1024 * 1024)} MB.", "artwork");

    /// <summary>
    /// Renders and stores the cover and stages the asset rows and the track pointer; the caller saves.
    /// Nothing is written to storage when the image is rejected.
    /// </summary>
    public async Task<Result<StagedArtwork>> StageAsync(Guid trackId, byte[] image, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (image.Length > settings.MaxArtworkBytes)
        {
            return TooLarge(settings.MaxArtworkBytes);
        }

        var rendered = processor.Render(image, ImageSpec.Cover(settings.MinArtworkSide), settings.MaxArtworkPixels);
        if (!rendered.IsSuccess)
        {
            return rendered.Error! with { Field = "artwork" };
        }

        var now = time.GetUtcNow();
        var small = MediaAsset.Create(trackId, MediaAssetKind.ArtworkSmall, MediaAsset.NewKey(trackId, MediaAssetKind.ArtworkSmall, ".jpg"), "image/jpeg",
            rendered.Value.Small.Length, null, null, null, null, null, null, now);
        var large = MediaAsset.Create(trackId, MediaAssetKind.ArtworkLarge, MediaAsset.NewKey(trackId, MediaAssetKind.ArtworkLarge, ".jpg"), "image/jpeg",
            rendered.Value.Large.Length, null, null, null, null, null, null, now);

        var written = new List<string>();
        try
        {
            foreach (var (asset, bytes) in new[] { (small, rendered.Value.Small), (large, rendered.Value.Large) })
            {
                using var stream = new MemoryStream(bytes, writable: false);
                await storage.PutAsync(asset.ObjectKey, stream, bytes.Length, asset.ContentType, cancellationToken);
                written.Add(asset.ObjectKey);
            }
        }
        catch
        {
            await DeleteQuietlyAsync(written);
            throw;
        }

        var replaced = (await repository.ListAssetsAsync(trackId, cancellationToken)).Where(a => MediaAsset.IsArtwork(a.Kind)).ToList();
        foreach (var old in replaced)
        {
            repository.Remove(old);
        }

        repository.Add(small);
        repository.Add(large);
        await catalog.StageArtworkAsync(trackId, large.Id, cancellationToken);
        return new StagedArtwork(large.Id, written, replaced.Select(a => a.ObjectKey).ToList());
    }

    /// <summary>Sets or replaces the cover of an owned track (any status).</summary>
    public async Task<Result<TrackArtworkDto>> ReplaceAsync(Guid userId, Guid trackId, byte[] image, CancellationToken cancellationToken)
    {
        if (await catalog.FindOwnedAsync(trackId, userId, cancellationToken) is null)
        {
            return TrackNotFound;
        }

        var staged = await StageAsync(trackId, image, cancellationToken);
        if (!staged.IsSuccess)
        {
            return staged.Error!;
        }

        await CommitAsync(staged.Value, cancellationToken);
        return ToDto(trackId, staged.Value.ArtworkId);
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid trackId, CancellationToken cancellationToken)
    {
        if (await catalog.FindOwnedAsync(trackId, userId, cancellationToken) is null)
        {
            return Result.Failure(TrackNotFound);
        }

        var assets = (await repository.ListAssetsAsync(trackId, cancellationToken)).Where(a => MediaAsset.IsArtwork(a.Kind)).ToList();
        foreach (var asset in assets)
        {
            repository.Remove(asset);
        }

        await catalog.StageArtworkAsync(trackId, null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await DeleteQuietlyAsync(assets.Select(a => a.ObjectKey));
        return Result.Success();
    }

    /// <summary>The rendition to serve, if the viewer may see the track (invisible → 404, like the track itself).</summary>
    public async Task<Result<MediaAsset>> ResolveAsync(Guid trackId, string size, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        MediaAssetKind? kind = size switch
        {
            TrackArtworkPaths.Small => MediaAssetKind.ArtworkSmall,
            TrackArtworkPaths.Large => MediaAssetKind.ArtworkLarge,
            _ => null,
        };
        if (kind is null || !await catalog.CanViewAsync(trackId, viewerUserId, viewerIsModerator, cancellationToken))
        {
            return kind is null ? ArtworkNotFound : TrackNotFound;
        }

        return await repository.FindAssetAsync(trackId, kind.Value, cancellationToken) is { } asset ? asset : ArtworkNotFound;
    }

    public static TrackArtworkDto ToDto(Guid trackId, Guid artworkId) =>
        new(TrackArtworkPaths.For(trackId, artworkId, TrackArtworkPaths.Small), TrackArtworkPaths.For(trackId, artworkId, TrackArtworkPaths.Large));

    /// <summary>Reads at most <paramref name="maxBytes"/>; returns null as soon as the body is longer.</summary>
    public static async Task<byte[]?> ReadLimitedAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private async Task CommitAsync(StagedArtwork staged, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteQuietlyAsync(staged.NewKeys);
            throw;
        }

        await DeleteQuietlyAsync(staged.ReplacedKeys);
    }

    public async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogOrphan(logger, ex, key);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete object {Key}; it is orphaned")]
    private static partial void LogOrphan(ILogger logger, Exception ex, string key);
}
