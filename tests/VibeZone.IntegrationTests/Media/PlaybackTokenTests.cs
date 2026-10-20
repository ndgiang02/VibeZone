using System.Net;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

/// <summary>
/// Native players (just_audio / ExoPlayer / AVPlayer) fetch the stream URL themselves, often from a background
/// service, so <c>GET /playback</c> hands out URLs that work without headers or cookies (ADR-0008).
/// </summary>
[Collection(MediaTests.Name)]
public sealed class PlaybackTokenTests(MediaFixture media, PostgresFixture postgres, MailpitFixture mail)
{
    private static async Task<JsonElement> PlaybackAsync(AppUser user, Guid trackId)
    {
        using var response = await user.SendAsync(HttpMethod.Get, $"/api/v1/tracks/{trackId}/playback");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return await response.DataAsync();
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient player, string url, string? range = "bytes=0-")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (range is not null)
        {
            request.Headers.TryAddWithoutValidation("Range", range);
        }

        var response = await player.SendAsync(request, AppUser.Ct);
        await response.Content.LoadIntoBufferAsync(AppUser.Ct);
        return response;
    }

    [Fact]
    public async Task Owner_gets_urls_for_a_private_track_that_a_credential_less_player_can_seek_through()
    {
        using var owner = await media.UserAsync();
        var track = await owner.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(3)), visibility: "Private");
        var id = track.GetProperty("id").GetGuid();

        var playback = await PlaybackAsync(owner, id);
        Assert.Equal(id, playback.GetProperty("trackId").GetGuid());
        Assert.Equal("audio/mp4", playback.GetProperty("contentType").GetString());
        Assert.InRange(playback.GetProperty("durationMs").GetInt32(), 2900, 3200);
        Assert.True(playback.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddHours(5));
        var streamUrl = playback.GetProperty("streamUrl").GetString()!;
        Assert.StartsWith($"/api/v1/tracks/{id}/stream?token=vzp_", streamUrl, StringComparison.Ordinal);

        using var player = media.Anonymous(); // no cookie, no Authorization header
        using var head = await GetAsync(player, streamUrl, "bytes=0-1023");
        Assert.Equal(HttpStatusCode.PartialContent, head.StatusCode);
        var size = head.Content.Headers.ContentRange!.Length!.Value;
        Assert.Equal(playback.GetProperty("sizeBytes").GetInt64(), size);
        using var seek = await GetAsync(player, streamUrl, $"bytes={size / 2}-");
        Assert.Equal(HttpStatusCode.PartialContent, seek.StatusCode);

        using var waveform = await GetAsync(player, playback.GetProperty("waveformUrl").GetString()!, range: null);
        Assert.Equal(HttpStatusCode.OK, waveform.StatusCode);
        Assert.Equal(1000, (await waveform.DataAsync()).GetProperty("peaks").GetArrayLength());

        // Without the token the private track stays invisible.
        using var bare = await GetAsync(player, $"/api/v1/tracks/{id}/stream");
        Assert.Equal(HttpStatusCode.NotFound, bare.StatusCode);
    }

    [Fact]
    public async Task Anonymous_listener_gets_urls_for_a_public_track()
    {
        using var artist = await media.UserAsync();
        var id = (await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)))).GetProperty("id").GetGuid();
        using var anonymous = media.Anonymous();

        using var response = await anonymous.GetAsync($"/api/v1/tracks/{id}/playback", AppUser.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var stream = await GetAsync(anonymous, (await response.DataAsync()).GetProperty("streamUrl").GetString()!);
        Assert.Equal(HttpStatusCode.PartialContent, stream.StatusCode);
    }

    [Fact]
    public async Task Token_does_not_outlive_the_listeners_right_to_the_track()
    {
        using var owner = await media.UserAsync();
        using var listener = await media.UserAsync(artist: false);
        var id = (await owner.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)))).GetProperty("id").GetGuid();
        var streamUrl = (await PlaybackAsync(listener, id)).GetProperty("streamUrl").GetString()!;
        using var player = media.Anonymous();
        using (var before = await GetAsync(player, streamUrl))
        {
            Assert.Equal(HttpStatusCode.PartialContent, before.StatusCode);
        }

        using var hide = await owner.SendAsync(HttpMethod.Patch, $"/api/v1/tracks/{id}", new { title = "Now private", visibility = "Private" });
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);

        using var after = await GetAsync(player, streamUrl);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Tampered_or_foreign_tokens_are_rejected()
    {
        using var artist = await media.UserAsync();
        var a = (await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)), visibility: "Private")).GetProperty("id").GetGuid();
        var b = (await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)), visibility: "Private")).GetProperty("id").GetGuid();
        var tokenForA = (await PlaybackAsync(artist, a)).GetProperty("streamUrl").GetString()!.Split("?token=")[1];
        using var player = media.Anonymous();

        using var foreign = await GetAsync(player, $"/api/v1/tracks/{b}/stream?token={tokenForA}");
        using var tampered = await GetAsync(player, $"/api/v1/tracks/{a}/stream?token={tokenForA[..^4]}AAAA");
        using var garbage = await GetAsync(player, $"/api/v1/tracks/{a}/stream?token=not-a-token");

        foreach (var response in new[] { foreign, tampered, garbage })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("playback_token_invalid", await response.ProblemCodeAsync());
        }
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string>(media.Storage.Settings) { ["Media:PlaybackTokenLifetime"] = "00:00:02" }, mail);
        using var artist = await media.UserAsync();
        var id = (await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)))).GetProperty("id").GetGuid();
        using var shortLived = await media.UserAsync(artist: false, api: api);
        var streamUrl = (await PlaybackAsync(shortLived, id)).GetProperty("streamUrl").GetString()!;
        using var player = api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

        using (var fresh = await GetAsync(player, streamUrl))
        {
            Assert.Equal(HttpStatusCode.PartialContent, fresh.StatusCode);
        }

        await Task.Delay(TimeSpan.FromSeconds(3), AppUser.Ct);
        using var expired = await GetAsync(player, streamUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal("playback_token_invalid", await expired.ProblemCodeAsync());
    }
}
