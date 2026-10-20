using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Application.Tracks;

/// <summary>Self-serve artist profiles: any signed-in (hence email-verified) user can become an artist.</summary>
public sealed class ArtistService(ICatalogRepository repository, AlbumService albums, IUserRoles roles, TimeProvider time)
{
    public async Task<Result<ArtistDto>> CreateForUserAsync(Guid userId, CreateArtistRequest request, CancellationToken cancellationToken)
    {
        var slug = Artist.NormalizeSlug(request.Slug);
        var name = Artist.NormalizeName(request.Name);
        if (Artist.Validate(slug, name, request.Bio) is { } invalid)
        {
            return invalid;
        }

        if (await repository.FindArtistByUserAsync(userId, cancellationToken) is not null)
        {
            return DomainError.Conflict("artist_exists", "You already have an artist profile.");
        }

        if (await repository.ArtistSlugTakenAsync(slug, cancellationToken))
        {
            return DomainError.Conflict("artist_slug_taken", "This artist URL is taken.", "slug");
        }

        var artist = Artist.Create(userId, slug, name, request.Bio, time.GetUtcNow());
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
