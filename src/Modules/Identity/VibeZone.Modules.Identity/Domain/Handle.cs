using System.Text.RegularExpressions;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Domain;

/// <summary>Public, URL-safe username (e.g. vibezone.app/u/{handle}). Lower-case, 3–30 characters.</summary>
public static partial class Handle
{
    public const int MinLength = 3;
    public const int MaxLength = 30;

    /// <summary>Names that collide with routes, roles or the brand.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "admin", "administrator", "root", "system", "support", "help", "about", "terms", "privacy", "security",
        "api", "app", "auth", "login", "logout", "register", "signup", "signin", "settings", "account", "me",
        "search", "discover", "library", "upload", "studio", "notifications", "moderation", "moderator",
        "artists", "artist", "tracks", "track", "albums", "album", "playlists", "playlist", "genres", "users", "u",
        "vibezone", "official", "staff", "en", "vi", "null", "undefined", "anonymous",
    };

    public static string Normalize(string? handle) => (handle ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Validates an already-normalized handle.</summary>
    public static DomainError? Validate(string handle)
    {
        if (handle.Length is < MinLength or > MaxLength || !Pattern().IsMatch(handle))
        {
            return DomainError.Validation("handle_invalid", $"Handle must be {MinLength}-{MaxLength} characters: a-z, 0-9, '_' or '-', starting and ending with a letter or digit.", "handle");
        }

        return Reserved.Contains(handle)
            ? DomainError.Validation("handle_reserved", "This handle is reserved.", "handle")
            : null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
