using Microsoft.AspNetCore.DataProtection;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Infrastructure;

namespace VibeZone.UnitTests.Media;

public sealed class PlaybackTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly DataProtectionPlaybackTokens _tokens = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Round_trips_the_grant_until_it_expires()
    {
        var grant = new PlaybackGrant(Guid.NewGuid(), Guid.NewGuid(), ViewerIsModerator: true);
        var token = _tokens.Issue(grant, Now.AddHours(6));

        Assert.StartsWith("vzp_", token, StringComparison.Ordinal);
        Assert.Equal(grant, _tokens.Read(token, Now));
        Assert.Equal(grant, _tokens.Read(token, Now.AddHours(6).AddSeconds(-1)));
        Assert.Null(_tokens.Read(token, Now.AddHours(6)));
    }

    [Fact]
    public void Anonymous_grant_has_no_viewer()
    {
        var grant = new PlaybackGrant(Guid.NewGuid(), null, false);

        Assert.Equal(grant, _tokens.Read(_tokens.Issue(grant, Now.AddMinutes(1)), Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("vzp_")]
    [InlineData("vzp_not-base64!")]
    [InlineData("vza_abc")]
    public void Rejects_malformed_tokens(string token) => Assert.Null(_tokens.Read(token, Now));

    [Fact]
    public void Rejects_tampered_tokens_and_tokens_from_another_key_ring()
    {
        var token = _tokens.Issue(new PlaybackGrant(Guid.NewGuid(), null, false), Now.AddHours(1));
        var tampered = token[..^2] + (token[^2] == 'A' ? "BB" : "AA");
        var otherRing = new DataProtectionPlaybackTokens(new EphemeralDataProtectionProvider());

        Assert.Null(_tokens.Read(tampered, Now));
        Assert.Null(otherRing.Read(token, Now));
        Assert.Null(_tokens.Read(token + new string('A', 1100), Now));
    }
}
