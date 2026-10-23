using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

/// <summary>Upload accept/reject with real media, through the real worker (ffprobe + FFmpeg).</summary>
[Collection(MediaTests.Name)]
public sealed class UploadPipelineTests(MediaFixture media, PostgresFixture postgres, MailpitFixture mail)
{
    [Fact]
    public async Task Valid_upload_is_transcoded_to_a_ready_aac_rendition_with_waveform()
    {
        using var artist = await media.UserAsync();
        var trackId = await artist.CreateTrackAsync();

        using var upload = await artist.UploadAsync(trackId, TestAudio.Wav(TimeSpan.FromSeconds(5)));

        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var status = await upload.DataAsync();
        Assert.Equal(trackId, status.GetProperty("trackId").GetGuid());
        Assert.Equal("song.wav", status.GetProperty("fileName").GetString());

        var track = await artist.WaitForTrackAsync(trackId, "Ready", "Failed");
        Assert.Equal("Ready", track.GetProperty("status").GetString());
        Assert.True(track.GetProperty("playable").GetBoolean());
        Assert.InRange(track.GetProperty("durationMs").GetInt32(), 4900, 5200);

        using var stream = await artist.SendAsync(HttpMethod.Get, $"/api/v1/tracks/{trackId}/stream");
        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal("audio/mp4", stream.Content.Headers.ContentType?.MediaType);
        var audio = await stream.Content.ReadAsByteArrayAsync(AppUser.Ct);
        Assert.Equal("ftyp"u8.ToArray(), audio[4..8]); // MP4 container

        var waveform = await artist.GetDataAsync($"/api/v1/tracks/{trackId}/waveform");
        Assert.Equal(1000, waveform.GetProperty("peaks").GetArrayLength());
        Assert.Contains(waveform.GetProperty("peaks").EnumerateArray(), p => p.GetDouble() > 0.1);
        Assert.Equal("Completed", (await artist.GetDataAsync($"/api/v1/tracks/{trackId}/upload")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Renamed_non_audio_file_is_rejected_at_upload_and_the_track_stays_a_draft()
    {
        using var artist = await media.UserAsync();
        var trackId = await artist.CreateTrackAsync();

        using var upload = await artist.UploadAsync(trackId, TestAudio.RenamedPng(), fileName: "song.mp3");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal("unsupported_file_type", await upload.ProblemCodeAsync());
        Assert.Equal("Draft", (await artist.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Corrupt_audio_passes_the_sniff_but_fails_in_the_worker()
    {
        using var artist = await media.UserAsync();
        var trackId = await artist.CreateTrackAsync();

        using var upload = await artist.UploadAsync(trackId, TestAudio.CorruptWav());
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);

        var track = await artist.WaitForTrackAsync(trackId, "Ready", "Failed");
        Assert.Equal("Failed", track.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(track.GetProperty("processingError").GetString()));
        Assert.False(track.GetProperty("playable").GetBoolean());
        using var stream = await artist.SendAsync(HttpMethod.Get, $"/api/v1/tracks/{trackId}/stream");
        Assert.Equal(HttpStatusCode.NotFound, stream.StatusCode);
    }

    [Fact]
    public async Task Oversized_and_truncated_uploads_are_rejected()
    {
        const int limit = 1024 * 1024;
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string>(media.Storage.Settings) { ["Media:MaxUploadBytes"] = $"{limit}" }, mail);
        using var artist = await media.UserAsync(api: api);
        var trackId = await artist.CreateTrackAsync();
        var big = TestAudio.Wav(TimeSpan.FromSeconds(10)); // ~1.7 MB

        // Declared size over the limit: rejected before the body is read.
        using (var declared = await artist.UploadAsync(trackId, big))
        {
            Assert.Equal(HttpStatusCode.BadRequest, declared.StatusCode);
            Assert.Equal("file_too_large", await declared.ProblemCodeAsync());
        }

        // Declared size under the limit but the body keeps going: stopped while streaming.
        using (var streamed = await artist.UploadAsync(trackId, big, declaredLength: limit))
        {
            Assert.Equal(HttpStatusCode.BadRequest, streamed.StatusCode);
            Assert.Equal("file_too_large", await streamed.ProblemCodeAsync());
        }

        // Body shorter than declared (truncated by a proxy, say).
        using (var truncated = await artist.UploadAsync(trackId, big[..1000], declaredLength: 2000))
        {
            Assert.Equal(HttpStatusCode.BadRequest, truncated.StatusCode);
            Assert.Equal("upload_incomplete", await truncated.ProblemCodeAsync());
        }

        Assert.Equal("Draft", (await artist.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Only_the_owner_can_upload_check_or_cancel()
    {
        using var owner = await media.UserAsync();
        using var other = await media.UserAsync();
        var trackId = await owner.CreateTrackAsync();

        using var upload = await other.UploadAsync(trackId, TestAudio.Wav(TimeSpan.FromSeconds(2)));
        using var status = await other.SendAsync(HttpMethod.Get, $"/api/v1/tracks/{trackId}/upload");
        using var cancel = await other.SendAsync(HttpMethod.Delete, $"/api/v1/tracks/{trackId}/upload");
        using var anonymousClient = media.Anonymous();
        using var anonymous = await anonymousClient.PutAsync($"/api/v1/tracks/{trackId}/upload", new ByteArrayContent([1, 2, 3]), AppUser.Ct);

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
