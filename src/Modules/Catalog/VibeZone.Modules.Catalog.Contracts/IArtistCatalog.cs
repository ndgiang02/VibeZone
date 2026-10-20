namespace VibeZone.Modules.Catalog.Contracts;

public enum ArtistImageSlot
{
    /// <summary>Square profile picture.</summary>
    Avatar,

    /// <summary>Wide (3:1) cover image at the top of the artist page.</summary>
    Banner,
}

/// <summary>
/// Artist profiles for the Media module (which stores their images). <c>Stage*</c> only records the change in
/// the shared unit of work; the caller commits (ADR-0001).
/// </summary>
public interface IArtistCatalog
{
    /// <summary>The artist profile owned by the user, if any.</summary>
    Task<Guid?> FindArtistIdForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid artistId, CancellationToken cancellationToken);

    /// <summary>Points the profile at its current image of that slot (an opaque version id), or clears it.</summary>
    Task StageImageAsync(Guid artistId, ArtistImageSlot slot, Guid? imageId, CancellationToken cancellationToken);
}

/// <summary>Public artist image URLs (served by the Media module). The version makes a changed image a new URL.</summary>
public static class ArtistImagePaths
{
    public static string For(Guid artistId, ArtistImageSlot slot, Guid imageId, string size) =>
        $"/api/v1/artists/{artistId}/{(slot == ArtistImageSlot.Avatar ? "avatar" : "banner")}/{size}?v={imageId:N}";
}
