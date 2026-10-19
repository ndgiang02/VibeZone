using System.Text.RegularExpressions;

namespace VibeZone.Modules.Catalog.Domain;

/// <summary>Curated genre taxonomy (reference data, not user-generated).</summary>
public sealed partial class Genre
{
    public const int MaxSlugLength = 64;
    public const int MaxNameLength = 64;

    public Genre(int id, string slug, string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > MaxSlugLength || !SlugPattern().IsMatch(slug))
        {
            throw new ArgumentException($"Genre slug '{slug}' must be lower-case kebab-case, at most {MaxSlugLength} characters.", nameof(slug));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Genre name must be at most {MaxNameLength} characters.", nameof(name));
        }

        Id = id;
        Slug = slug;
        Name = name.Trim();
    }

    public int Id { get; private set; }

    public string Slug { get; private set; }

    public string Name { get; private set; }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
