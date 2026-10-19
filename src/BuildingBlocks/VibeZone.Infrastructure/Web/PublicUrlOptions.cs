using System.ComponentModel.DataAnnotations;

namespace VibeZone.Infrastructure.Web;

/// <summary>Public origin of the web app, used to build links in emails (never derived from request headers).</summary>
public sealed class PublicUrlOptions
{
    public const string Section = "App";

    [Required]
    [Url]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string Link(string locale, string path, IReadOnlyDictionary<string, string> query)
    {
        var qs = string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{PublicBaseUrl.TrimEnd('/')}/{locale}/{path.TrimStart('/')}{(qs.Length > 0 ? "?" + qs : string.Empty)}";
    }
}
