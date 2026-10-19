using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Domain;

/// <summary>Public-facing account details and preferences (1:1 with the identity user).</summary>
public sealed class UserProfile
{
    public const int MaxDisplayNameLength = 60;

    private UserProfile() { }

    public static UserProfile Create(Guid userId, string displayName, string locale, DateTimeOffset now) => new()
    {
        UserId = userId,
        DisplayName = displayName,
        Locale = Locales.Normalize(locale),
        CreatedAt = now,
        UpdatedAt = now,
    };

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Preferred UI/email language (see <see cref="Locales"/>).</summary>
    public string Locale { get; private set; } = Locales.Default;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static string NormalizeDisplayName(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static DomainError? ValidateDisplayName(string normalized) =>
        normalized.Length is 0 or > MaxDisplayNameLength
            ? DomainError.Validation("display_name_invalid", $"Display name must be 1-{MaxDisplayNameLength} characters.", "displayName")
            : null;

    public void Update(string displayName, string locale, DateTimeOffset now)
    {
        DisplayName = displayName;
        Locale = Locales.Normalize(locale);
        UpdatedAt = now;
    }
}
