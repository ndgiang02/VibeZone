using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Domain;

public enum ArtistImageKind
{
    BannerSmall,
    BannerLarge,
}

/// <summary>A stored rendition of an artist's banner (JPEG made by the API from the upload).</summary>
public sealed class ArtistImage
{
    private ArtistImage() { }

    public static ArtistImage Create(Guid artistId, ArtistImageKind kind, long sizeBytes, DateTimeOffset now)
    {
        var id = EntityId.New();
        return new()
        {
            Id = id,
            ArtistId = artistId,
            Kind = kind,
            // Server-generated key; user input never reaches object keys.
            ObjectKey = $"artists/{artistId:N}/{kind.ToString().ToLowerInvariant()}/{id:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = sizeBytes,
            CreatedAt = now,
        };
    }

    public Guid Id { get; private set; }

    public Guid ArtistId { get; private set; }

    public ArtistImageKind Kind { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
