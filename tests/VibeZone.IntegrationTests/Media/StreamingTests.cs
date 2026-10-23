using System.Net;
using System.Net.Http.Headers;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

/// <summary>RFC 9110 range semantics and authorization-before-bytes on the stream endpoint.</summary>
[Collection(MediaTests.Name)]
public sealed class StreamingTests(MediaFixture media)
{
    private static Task<HttpResponseMessage> GetRangeAsync(HttpClient client, string path, string? range)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (range is not null)
        {
            request.Headers.TryAddWithoutValidation("Range", range);
        }

        return client.SendAsync(request, AppUser.Ct);
    }

    [Fact]
    public async Task Range_requests_follow_rfc_9110()
    {
        using var artist = await media.UserAsync();
        var track = await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(4)));
        var path = $"/api/v1/tracks/{track.GetProperty("id").GetGuid()}/stream";
        using var client = media.Anonymous(); // public track: no credentials needed

        using var full = await GetRangeAsync(client, path, null);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal("bytes", full.Headers.AcceptRanges.Single());
        var bytes = await full.Content.ReadAsByteArrayAsync(AppUser.Ct);
        var size = bytes.Length;
        Assert.Equal(size, full.Content.Headers.ContentLength);
        Assert.True(size > 1000);

        using (var open = await GetRangeAsync(client, path, "bytes=0-"))
        {
            Assert.Equal(HttpStatusCode.PartialContent, open.StatusCode);
            Assert.Equal(new ContentRangeHeaderValue(0, size - 1, size), open.Content.Headers.ContentRange);
            Assert.Equal(bytes, await open.Content.ReadAsByteArrayAsync(AppUser.Ct));
        }

        using (var middle = await GetRangeAsync(client, path, "bytes=100-199"))
        {
            Assert.Equal(HttpStatusCode.PartialContent, middle.StatusCode);
            Assert.Equal(new ContentRangeHeaderValue(100, 199, size), middle.Content.Headers.ContentRange);
            Assert.Equal(100, middle.Content.Headers.ContentLength);
            Assert.Equal(bytes[100..200], await middle.Content.ReadAsByteArrayAsync(AppUser.Ct));
        }

        using (var suffix = await GetRangeAsync(client, path, "bytes=-500"))
        {
            Assert.Equal(HttpStatusCode.PartialContent, suffix.StatusCode);
            Assert.Equal(new ContentRangeHeaderValue(size - 500, size - 1, size), suffix.Content.Headers.ContentRange);
            Assert.Equal(bytes[^500..], await suffix.Content.ReadAsByteArrayAsync(AppUser.Ct));
        }

        using (var pastEnd = await GetRangeAsync(client, path, $"bytes={size - 10}-{size + 1000}"))
        {
            Assert.Equal(HttpStatusCode.PartialContent, pastEnd.StatusCode); // clamped to the last byte
            Assert.Equal(new ContentRangeHeaderValue(size - 10, size - 1, size), pastEnd.Content.Headers.ContentRange);
        }

        using (var unsatisfiable = await GetRangeAsync(client, path, $"bytes={size}-"))
        {
            Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, unsatisfiable.StatusCode);
            Assert.Equal($"bytes */{size}", unsatisfiable.Content.Headers.ContentRange?.ToString());
        }
    }

    [Fact]
    public async Task Private_track_streams_only_for_its_owner_and_is_invisible_to_everyone_else()
    {
        using var owner = await media.UserAsync();
        using var other = await media.UserAsync(artist: false);
        var track = await owner.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)), visibility: "Private");
        var id = track.GetProperty("id").GetGuid();
        using var anonymous = media.Anonymous();

        using (var mine = await owner.SendAsync(HttpMethod.Get, $"/api/v1/tracks/{id}/stream"))
        {
            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        }

        foreach (var path in new[] { $"/api/v1/tracks/{id}", $"/api/v1/tracks/{id}/stream", $"/api/v1/tracks/{id}/waveform", $"/api/v1/tracks/{id}/playback" })
        {
            using var byOther = await other.SendAsync(HttpMethod.Get, path);
            using var byAnonymous = await anonymous.GetAsync(path, AppUser.Ct);
            Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, byAnonymous.StatusCode);
            Assert.Equal("track_not_found", await byOther.ProblemCodeAsync());
        }

        using var mineList = await owner.SendAsync(HttpMethod.Get, "/api/v1/tracks?pageSize=50");
        Assert.DoesNotContain((await mineList.DataAsync()).EnumerateArray(), t => t.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Public_track_streams_anonymously_and_appears_in_new_releases()
    {
        using var artist = await media.UserAsync();
        var track = await artist.UploadReadyAsync(TestAudio.Wav(TimeSpan.FromSeconds(2)));
        var id = track.GetProperty("id").GetGuid();
        using var anonymous = media.Anonymous();

        using var stream = await GetRangeAsync(anonymous, $"/api/v1/tracks/{id}/stream", "bytes=0-1023");
        Assert.Equal(HttpStatusCode.PartialContent, stream.StatusCode);

        using var releases = await anonymous.GetAsync("/api/v1/tracks?page=1&pageSize=5", AppUser.Ct);
        var body = await releases.JsonAsync();
        Assert.Equal(id, body.GetProperty("data")[0].GetProperty("id").GetGuid()); // newest first
        var meta = body.GetProperty("meta");
        Assert.Equal(1, meta.GetProperty("page").GetInt32());
        Assert.Equal(5, meta.GetProperty("pageSize").GetInt32());
        Assert.True(meta.GetProperty("total").GetInt32() >= 1);
    }
}
