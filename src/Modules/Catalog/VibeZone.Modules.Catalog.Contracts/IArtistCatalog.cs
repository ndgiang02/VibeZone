namespace VibeZone.Modules.Catalog.Contracts;

/// <summary>
/// Artist profiles for the Media module (which stores their banners). <c>Stage*</c> only records the change in
/// the shared unit of work; the caller commits (ADR-0001). The profile picture is the account's (ADR-0013).
/// </summary>
public interface IArtistCatalog
{
    /// <summary>The artist profile owned by the user, if any.</summary>
    Task<Guid?> FindArtistIdForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid artistId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ArtistSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> artistIds, CancellationToken cancellationToken);

    /// <summary>Points the profile at its current banner (an opaque version id), or clears it.</summary>
    Task StageBannerAsync(Guid artistId, Guid? imageId, CancellationToken cancellationToken);
}

/// <summary>Public artist banner URLs (served by the Media module). The version makes a changed image a new URL.</summary>
public static class ArtistImagePaths
{
    public static string Banner(Guid artistId, Guid imageId, string size) => $"/api/v1/artists/{artistId}/banner/{size}?v={imageId:N}";
}
