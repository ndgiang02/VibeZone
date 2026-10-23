using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Domain;

public enum UserImageKind
{
    AvatarSmall,
    AvatarLarge,
}

/// <summary>A stored rendition of an account picture (JPEG made by the API from the upload).</summary>
public sealed class UserImage
{
    private UserImage() { }

    public static UserImage Create(Guid userId, UserImageKind kind, long sizeBytes, DateTimeOffset now)
    {
        var id = EntityId.New();
        return new()
        {
            Id = id,
            UserId = userId,
            Kind = kind,
            // Server-generated key; user input never reaches object keys.
            ObjectKey = $"users/{userId:N}/{kind.ToString().ToLowerInvariant()}/{id:N}.jpg",
            ContentType = "image/jpeg",
            SizeBytes = sizeBytes,
            CreatedAt = now,
        };
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public UserImageKind Kind { get; private set; }

    public string ObjectKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
