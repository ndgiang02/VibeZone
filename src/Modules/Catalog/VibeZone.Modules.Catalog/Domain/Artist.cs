using System.Text.RegularExpressions;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Catalog.Domain;

/// <summary>An account's public artist identity (self-serve, one per account in MVP).</summary>
public sealed partial class Artist
{
    public const int MinSlugLength = 3;
    public const int MaxSlugLength = 40;
    public const int MaxNameLength = 80;
    public const int MaxBioLength = 1000;

    private Artist() { }

    public static Artist Create(Guid userId, string slug, string name, string? bio, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        UserId = userId,
        Slug = slug,
        Name = name,
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim(),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Bio { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Version of the current profile picture (stored by the Media module), or null.</summary>
    public Guid? AvatarId { get; private set; }

    /// <summary>Version of the current banner (stored by the Media module), or null.</summary>
    public Guid? BannerId { get; private set; }

    public void SetAvatar(Guid? imageId, DateTimeOffset now)
    {
        AvatarId = imageId;
        UpdatedAt = now;
    }

    public void SetBanner(Guid? imageId, DateTimeOffset now)
    {
        BannerId = imageId;
        UpdatedAt = now;
    }

    public static string NormalizeSlug(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeName(string? name) => string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static DomainError? Validate(string slug, string name, string? bio)
    {
        if (slug.Length is < MinSlugLength or > MaxSlugLength || !SlugPattern().IsMatch(slug))
        {
            return DomainError.Validation("artist_slug_invalid", $"Artist URL must be {MinSlugLength}-{MaxSlugLength} characters: a-z, 0-9 and single hyphens.", "slug");
        }

        if (name.Length is 0 or > MaxNameLength)
        {
            return DomainError.Validation("artist_name_invalid", $"Artist name must be 1-{MaxNameLength} characters.", "name");
        }

        return bio is { Length: > MaxBioLength }
            ? DomainError.Validation("artist_bio_too_long", $"Bio must be at most {MaxBioLength} characters.", "bio")
            : null;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
