using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

/// <summary>
/// Self-serve artist profiles: any signed-in (hence email-verified) user can become an artist. The profile is the
/// account's public face — its name, URL and picture are the account's (ADR-0013).
/// </summary>
public sealed class ArtistService(ICatalogRepository repository, AlbumService albums, IUserRoles roles, IUserDirectory users, TimeProvider time)
{
    public async Task<Result<ArtistDto>> CreateForUserAsync(Guid userId, CreateArtistRequest request, CancellationToken cancellationToken)
    {
        if (Artist.ValidateBio(request.Bio) is { } invalid)
        {
            return invalid;
        }

        if (await repository.FindArtistByUserAsync(userId, cancellationToken) is not null)
        {
            return DomainError.Conflict("artist_exists", "You already have an artist profile.");
        }

        if (await users.FindProfileAsync(userId, cancellationToken) is not { } profile)
        {
            return DomainError.NotFound("user_not_found", "User not found.");
        }

        var artist = Artist.Create(userId, profile.Handle, profile.DisplayName, profile.AvatarId, request.Bio, time.GetUtcNow());
        repository.Add(artist);
        await repository.SaveChangesAsync(cancellationToken);
        await roles.GrantAsync(userId, Roles.Artist, cancellationToken);
        return artist.ToDto();
    }

    public async Task<Result<ArtistDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await repository.FindArtistByUserAsync(userId, cancellationToken) is { } artist
            ? artist.ToDto()
            : DomainError.NotFound("artist_not_found", "No artist profile yet.");

    /// <summary>Public profile: only tracks the viewer may see.</summary>
    public async Task<Result<ArtistProfileDto>> GetProfileAsync(string slug, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken)
    {
        var artist = await repository.FindArtistBySlugAsync(Artist.NormalizeSlug(slug), cancellationToken);
        if (artist is null)
        {
            return DomainError.NotFound("artist_not_found", "Artist not found.");
        }

        var isOwner = viewerUserId == artist.UserId;
        var tracks = (await repository.ListArtistTracksAsync(artist.Id, cancellationToken))
            .Where(r => r.Track.IsVisibleTo(isOwner, viewerIsModerator))
            .Where(r => isOwner || r.Track.Visibility == TrackVisibility.Public) // unlisted: link only
            .Select(r => r.ToDto(isOwner))
            .ToList();
        return new ArtistProfileDto(artist.ToDto(), tracks, await albums.ListForArtistAsync(artist, isOwner, viewerIsModerator, cancellationToken));
    }
}
