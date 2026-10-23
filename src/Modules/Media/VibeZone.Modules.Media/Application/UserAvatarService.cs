using Microsoft.Extensions.Options;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>
/// The signed-in user's account picture (any user, artist or not). Same pipeline as artist avatars (ADR-0009):
/// re-encoded square JPEG 160/640 px, private storage, original not kept.
/// </summary>
public sealed class UserAvatarService(
    IUserAvatars profiles,
    IMediaRepository repository,
    IObjectStorage storage,
    IArtworkProcessor processor,
    ArtworkService artwork,
    IOptions<MediaOptions> options,
    TimeProvider time)
{
    private static readonly DomainError ImageNotFound = DomainError.NotFound("image_not_found", "This user has no picture.");

    public async Task<Result<ImageUrlsDto>> ReplaceAsync(Guid userId, byte[] image, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (image.Length > settings.MaxArtworkBytes)
        {
            return ArtistImageService.TooLarge(settings.MaxArtworkBytes);
        }

        var rendered = processor.Render(image, ImageSpec.Avatar(settings.MinArtworkSide), settings.MaxArtworkPixels);
        if (!rendered.IsSuccess)
        {
            var error = rendered.Error!;
            return error with { Code = error.Code.Replace("artwork_", "image_", StringComparison.Ordinal) };
        }

        var now = time.GetUtcNow();
        var small = UserImage.Create(userId, UserImageKind.AvatarSmall, rendered.Value.Small.Length, now);
        var large = UserImage.Create(userId, UserImageKind.AvatarLarge, rendered.Value.Large.Length, now);
        var written = new List<string>();
        try
        {
            foreach (var (row, bytes) in new[] { (small, rendered.Value.Small), (large, rendered.Value.Large) })
            {
                using var stream = new MemoryStream(bytes, writable: false);
                await storage.PutAsync(row.ObjectKey, stream, bytes.Length, row.ContentType, cancellationToken);
                written.Add(row.ObjectKey);
            }

            var replaced = await repository.ListUserImagesAsync(userId, cancellationToken);
            foreach (var old in replaced)
            {
                repository.Remove(old);
            }

            repository.Add(small);
            repository.Add(large);
            await profiles.StageAvatarAsync(userId, large.Id, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            await artwork.DeleteQuietlyAsync(replaced.Select(i => i.ObjectKey));
        }
        catch
        {
            await artwork.DeleteQuietlyAsync(written);
            throw;
        }

        return new ImageUrlsDto(UserAvatarPaths.For(userId, large.Id, "small"), UserAvatarPaths.For(userId, large.Id, "large"));
    }

    public async Task<Result> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var images = await repository.ListUserImagesAsync(userId, cancellationToken);
        foreach (var image in images)
        {
            repository.Remove(image);
        }

        await profiles.StageAvatarAsync(userId, null, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await artwork.DeleteQuietlyAsync(images.Select(i => i.ObjectKey));
        return Result.Success();
    }

    public async Task<Result<UserImage>> ResolveAsync(Guid userId, string size, CancellationToken cancellationToken)
    {
        UserImageKind? kind = size switch
        {
            "small" => UserImageKind.AvatarSmall,
            "large" => UserImageKind.AvatarLarge,
            _ => null,
        };
        return kind is { } k && await repository.FindUserImageAsync(userId, k, cancellationToken) is { } image ? image : ImageNotFound;
    }
}
