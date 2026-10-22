namespace VibeZone.Modules.Catalog.Contracts;

/// <summary>Display and ownership facts about a track for other modules (no visibility filtering: callers check).</summary>
/// <param name="OwnerUserId">The account behind the track's artist profile.</param>
/// <param name="ArtworkUrl">Small cover, or null.</param>
public sealed record TrackInfo(
    Guid TrackId,
    string Title,
    Guid ArtistId,
    string ArtistName,
    string ArtistSlug,
    Guid OwnerUserId,
    string? ArtworkUrl,
    int? DurationMs,
    bool TakenDown);

/// <summary>Public facts about an artist profile.</summary>
public sealed record ArtistSummary(Guid ArtistId, string Slug, string Name, string? AvatarUrl, Guid OwnerUserId);
