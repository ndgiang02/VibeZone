namespace VibeZone.SharedKernel;

/// <summary>UI/email languages. Keep in sync with apps/web/src/i18n/routing.ts.</summary>
public static class Locales
{
    public const string Vietnamese = "vi";
    public const string English = "en";
    public const string Default = Vietnamese;

    public static readonly IReadOnlyList<string> Supported = [Vietnamese, English];

    public static string Normalize(string? locale) =>
        locale is not null && Supported.Contains(locale.Trim().ToLowerInvariant()) ? locale.Trim().ToLowerInvariant() : Default;
}
