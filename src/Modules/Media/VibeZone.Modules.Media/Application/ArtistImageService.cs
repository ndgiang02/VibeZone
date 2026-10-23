using Microsoft.Extensions.Options;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>Image URLs in two sizes, returned after a change (the same shape the DTOs carry).</summary>
public sealed record ImageUrlsDto(string Small, string Large);

/// <summary>
/// The signed-in artist's banner. Same pipeline as track covers (ADR-0009): decoded and re-encoded by the API,
/// stored privately under server keys, original not kept; public to everyone (artist profiles are public). The
/// artist's picture is the account picture (<see cref="UserAvatarService"/>, ADR-0013).
/// </summary>
public sealed class ArtistImageService(
    IArtistCatalog catalog,
    IMediaRepository repository,
    IObjectStorage storage,
    IArtworkProcessor processor,
    ArtworkService artwork,
    IOptions<MediaOptions> options,
    TimeProvider time)
{
    private static readonly DomainError ArtistNotFound = DomainError.NotFound("artist_not_found", "No artist profile yet.");
    private static readonly DomainError ImageNotFound = DomainError.NotFound("image_not_found", "This artist has no such image.");

    public static DomainError TooLarge(long maxBytes) =>
        DomainError.Validation("image_too_large", $"Images can be at most {maxBytes / (1024 * 1024)} MB.");

    public async Task<Result<ImageUrlsDto>> ReplaceAsync(Guid userId, byte[] image, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (await catalog.FindArtistIdForUserAsync(userId, cancellationToken) is not { } artistId)
        {
            return ArtistNotFound;
        }

        if (image.Length > settings.MaxArtworkBytes)
        {
            return TooLarge(settings.MaxArtworkBytes);
        }

        var rendered = processor.Render(image, ImageSpec.Banner(settings.MinArtworkSide), settings.MaxArtworkPixels);
        if (!rendered.IsSuccess)
        {
            // The processor speaks in track-cover terms; artist images have their own codes.
            var error = rendered.Error!;
            return error with { Code = error.Code.Replace("artwork_", "image_", StringComparison.Ordinal) };
        }

        var now = time.GetUtcNow();
        var small = ArtistImage.Create(artistId, ArtistImageKind.BannerSmall, rendered.Value.Small.Length, now);
        var large = ArtistImage.Create(artistId, ArtistImageKind.BannerLarge, rendered.Value.Large.Length, now);
        var written = new List<string>();
        try
        {
            foreach (var (row, bytes) in new[] { (small, rendered.Value.Small), (large, rendered.Value.Large) })
            {
                using var stream = new MemoryStream(bytes, writable: false);
                await storage.PutAsync(row.ObjectKey, stream, bytes.Length, row.ContentType, cancellationToken);
                written.Add(row.ObjectKey);
            }

            var replaced = await repository.ListArtistImagesAsync(artistId, cancellationToken);
            foreach (var old in replaced)
            {
                repository.Remove(old);
            }

            repository.Add(small);
            repository.Add(large);
            await catalog.StageBannerAsync(artistId, large.Id, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            await artwork.DeleteQuietlyAsync(replaced.Select(i => i.ObjectKey));
        }
        catch
        {
            await artwork.DeleteQuietlyAsync(written);
            throw;
        }

        return new ImageUrlsDto(ArtistImagePaths.Banner(artistId, large.Id, "small"), ArtistImagePaths.Banner(artistId, large.Id, "large"));
    }

    public async Task<Result> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await catalog.FindArtistIdForUserAsync(userId, cancellationToken) is not { } artistId)
        {
            return Result.Failure(ArtistNotFound);
        }

        var images = await repository.ListArtistImagesAsync(artistId, cancellationToken);
        foreach (var image in images)
        {
            repository.Remove(image);
        }

        await catalog.StageBannerAsync(artistId, null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await artwork.DeleteQuietlyAsync(images.Select(i => i.ObjectKey));
        return Result.Success();
    }

    public async Task<Result<ArtistImage>> ResolveAsync(Guid artistId, string size, CancellationToken cancellationToken)
    {
        ArtistImageKind? kind = size switch
        {
            "small" => ArtistImageKind.BannerSmall,
            "large" => ArtistImageKind.BannerLarge,
            _ => null,
        };
        return kind is { } k && await repository.FindArtistImageAsync(artistId, k, cancellationToken) is { } image ? image : ImageNotFound;
    }
}
