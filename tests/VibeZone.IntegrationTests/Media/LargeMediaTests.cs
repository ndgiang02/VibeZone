using System.Net;
using System.Net.Http.Headers;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

[Collection(MediaTests.Name)]
public sealed class LargeMediaTests(MediaFixture media)
{
    [Fact]
    public async Task Thirty_minute_upload_over_50_mb_becomes_playable_and_streams_without_buffering_in_memory()
    {
        using var artist = await media.UserAsync();
        var original = TestAudio.Wav(TimeSpan.FromMinutes(30), sampleRate: 22_050, channels: 1);
        Assert.True(original.Length >= 50 * 1024 * 1024, $"Upload is {original.Length:N0} bytes; the check needs at least 50 MB.");

        var track = await artist.UploadReadyAsync(original, timeout: TimeSpan.FromMinutes(10));
        Assert.True(track.GetProperty("durationMs").GetInt32() >= (30 * 60 * 1000) - 1000);
        var path = $"/api/v1/tracks/{track.GetProperty("id").GetGuid()}/stream";
        using var player = media.Anonymous();

        // The first range tells the player the size; a seek to 3/4 then fetches just that range.
        long size;
        using (var first = new HttpRequestMessage(HttpMethod.Get, path))
        {
            first.Headers.Range = new RangeHeaderValue(0, 65_535);
            using var response = await player.SendAsync(first, AppUser.Ct);
            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            size = response.Content.Headers.ContentRange!.Length!.Value;
        }

        // (A pure tone compresses far below the 256 kbps target, so the rendition is smaller than real music.)
        Assert.True(size >= 10L * 1024 * 1024, $"Rendition is only {size:N0} bytes.");
        using (var seek = new HttpRequestMessage(HttpMethod.Get, path))
        {
            seek.Headers.Range = new RangeHeaderValue(size * 3 / 4, (size * 3 / 4) + 65_535);
            using var response = await player.SendAsync(seek, AppUser.Ct);
            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            Assert.Equal(65_536, (await response.Content.ReadAsByteArrayAsync(AppUser.Ct)).Length);
        }

        // Full download read in small chunks: the API must not allocate anything like the file size.
        // (Media tests run serially and the worker is out of process, so this process's allocations are ours.)
        GC.Collect();
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long received = 0;
        using (var response = await player.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, AppUser.Ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var body = await response.Content.ReadAsStreamAsync(AppUser.Ct);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await body.ReadAsync(buffer, AppUser.Ct)) > 0)
            {
                received += read;
            }
        }

        Assert.Equal(size, received);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        Assert.True(allocated < received / 4, $"Streaming {received:N0} bytes allocated {allocated:N0} bytes.");
    }
}
