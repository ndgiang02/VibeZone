using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Domain;

/// <summary>
/// An account's artist profile (self-serve, one per account). Its name, URL and picture are the account's display
/// name, handle and picture — copies kept in sync by Identity (ADR-0013); only bio and banner are its own.
/// </summary>
public sealed class Artist
{
    public const int MaxSlugLength = 40;
    public const int MaxNameLength = 80;
    public const int MaxBioLength = 1000;

    private Artist() { }

    public static Artist Create(Guid userId, string handle, string displayName, Guid? avatarId, string? bio, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        UserId = userId,
        Slug = handle,
        Name = displayName,
        AvatarId = avatarId,
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim(),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>The account's handle (immutable).</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>The account's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Bio { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Version of the account picture (stored by the Media module under the user), or null.</summary>
    public Guid? AvatarId { get; private set; }

    /// <summary>Version of the current banner (stored by the Media module), or null.</summary>
    public Guid? BannerId { get; private set; }

    /// <summary>The account's display name or picture changed.</summary>
    public void SyncAccount(string displayName, Guid? avatarId, DateTimeOffset now)
    {
        Name = displayName;
        AvatarId = avatarId;
        UpdatedAt = now;
    }

    public void SetBanner(Guid? imageId, DateTimeOffset now)
    {
        BannerId = imageId;
        UpdatedAt = now;
    }

    public static string NormalizeSlug(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    public static DomainError? ValidateBio(string? bio) =>
        bio is { Length: > MaxBioLength }
            ? DomainError.Validation("artist_bio_too_long", $"Bio must be at most {MaxBioLength} characters.", "bio")
            : null;
}
