using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

[Collection(MediaTests.Name)]
public sealed class WorkerRecoveryTests(MediaFixture media)
{
    [Fact]
    public async Task Job_of_a_killed_worker_is_reclaimed_after_its_lease_expires_and_completes()
    {
        using var artist = await media.UserAsync();
        var trackId = await artist.CreateTrackAsync();
        // Long enough (10 min of audio) that the worker is still busy when it is killed.
        using var upload = await artist.UploadAsync(trackId, TestAudio.Wav(TimeSpan.FromMinutes(10), sampleRate: 22_050, channels: 1));
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);

        var deadline = DateTime.UtcNow.AddMinutes(1);
        while (true)
        {
            var job = await artist.GetDataAsync($"/api/v1/tracks/{trackId}/upload");
            if (job.GetProperty("status").GetString() == "Processing" && job.GetProperty("attempts").GetInt32() == 1)
            {
                break;
            }

            Assert.True(DateTime.UtcNow < deadline, "The worker never claimed the job.");
            await Task.Delay(200, AppUser.Ct);
        }

        await Task.Delay(TimeSpan.FromSeconds(1), AppUser.Ct); // let FFmpeg start
        await media.ReplaceWorkerAfterKillAsync();

        var track = await artist.WaitForTrackAsync(trackId, "Ready", "Failed");
        Assert.Equal("Ready", track.GetProperty("status").GetString());
        var final = await artist.GetDataAsync($"/api/v1/tracks/{trackId}/upload");
        Assert.Equal("Completed", final.GetProperty("status").GetString());
        Assert.Equal(2, final.GetProperty("attempts").GetInt32());
    }
}
