using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using VibeZone.Modules.Media.Application;

namespace VibeZone.Modules.Media.Infrastructure;

/// <summary>
/// Playback tokens protected (encrypted + authenticated) with the shared Data Protection key ring, like the
/// app's access tokens (ADR-0007/0008). They name one track and the viewer they were issued to.
/// </summary>
internal sealed class DataProtectionPlaybackTokens(IDataProtectionProvider provider) : IPlaybackTokens
{
    private const string Prefix = "vzp_";
    private const string Version = "v1";
    private const int MaxLength = 1024;

    private readonly IDataProtector protector = provider.CreateProtector("VibeZone.Media.PlaybackToken.v1");

    public string Issue(PlaybackGrant grant, DateTimeOffset expiresAt)
    {
        var viewer = grant.ViewerUserId?.ToString("N") ?? string.Empty;
        var expires = expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return Prefix + protector.Protect($"{Version}|{grant.TrackId:N}|{viewer}|{(grant.ViewerIsModerator ? 1 : 0)}|{expires}");
    }

    public PlaybackGrant? Read(string token, DateTimeOffset now)
    {
        if (token.Length > MaxLength || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string payload;
        try
        {
            payload = protector.Unprotect(token[Prefix.Length..]);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        var parts = payload.Split('|');
        if (parts.Length != 5 || parts[0] != Version ||
            !Guid.TryParseExact(parts[1], "N", out var trackId) ||
            !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            now.ToUnixTimeSeconds() >= expires)
        {
            return null;
        }

        Guid? viewer = null;
        if (parts[2].Length > 0)
        {
            if (!Guid.TryParseExact(parts[2], "N", out var viewerId))
            {
                return null;
            }

            viewer = viewerId;
        }

        return new PlaybackGrant(trackId, viewer, parts[3] == "1");
    }
}
