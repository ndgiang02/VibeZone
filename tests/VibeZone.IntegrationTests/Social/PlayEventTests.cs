using System.Net;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Social;

/// <summary>Play ingestion: idempotent events, the valid-listen rule (data-model.md) and the stats it feeds.</summary>
public sealed class PlayEventTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public PlayEventTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    [Fact]
    public async Task Duplicate_events_are_not_counted_twice()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, durationMs: 10_000);
        var (listener, _) = await _content.UserAsync();
        var session = Guid.NewGuid();
        var started = Event(session, track, "started", 0);
        var progress = Event(session, track, "progress", 6_000);

        Assert.Equal(2, await SendAsync(listener, null, started, progress));
        Assert.Equal(0, await SendAsync(listener, null, started, progress)); // re-sent batch
        Assert.Equal(0, await SendAsync(listener, null, progress, progress)); // duplicates within a batch

        var stats = await listener.GetDataAsync($"/api/v1/tracks/{track}/stats");
        Assert.Equal(1, stats.GetProperty("plays").GetInt64());
        Assert.Equal(1, stats.GetProperty("listens").GetInt64());
        Assert.Equal(2, await _content.ScalarAsync("SELECT count(*) FROM analytics.play_events WHERE session_id = @s", ("s", session)));
    }

    [Fact]
    public async Task Short_track_is_a_valid_listen_after_half_and_only_once_per_listener_in_the_window()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, durationMs: 10_000); // threshold: 5 s
        var (listener, _) = await _content.UserAsync();

        var first = Guid.NewGuid();
        Assert.Equal(2, await SendAsync(listener, null, Event(first, track, "started", 0), Event(first, track, "progress", 4_000)));
        Assert.Equal(0, (await StatsAsync(track)).Listens);
        Assert.Equal(1, await SendAsync(listener, null, Event(first, track, "completed", 9_000)));
        Assert.Equal(1, (await StatsAsync(track)).Listens);

        // Replaying it right away is a new play but not another valid listen (30-minute window).
        var second = Guid.NewGuid();
        Assert.Equal(2, await SendAsync(listener, null, Event(second, track, "started", 0), Event(second, track, "completed", 9_000)));
        Assert.Equal((2L, 1L), await StatsAsync(track));

        // Another listener counts.
        var (other, _) = await _content.UserAsync();
        var third = Guid.NewGuid();
        await SendAsync(other, null, Event(third, track, "started", 0), Event(third, track, "completed", 9_000));
        Assert.Equal((3L, 2L), await StatsAsync(track));

        // Both appear in the listener's history.
        Assert.Equal([track], (await listener.GetDataAsync("/api/v1/me/history")).EnumerateArray()
            .Select(h => h.GetProperty("track").GetProperty("id").GetGuid()).Distinct());
    }

    [Fact]
    public async Task Reported_listening_time_is_capped_by_wall_clock_time()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, durationMs: 240_000); // threshold: 30 s
        var (listener, _) = await _content.UserAsync();
        var session = Guid.NewGuid();

        // Claims 3 minutes of listening one moment after starting: capped at elapsed time + 10 s slack.
        await SendAsync(listener, null, Event(session, track, "started", 0), Event(session, track, "progress", 180_000));

        Assert.Equal((1L, 0L), await StatsAsync(track));
        Assert.InRange(await _content.ScalarAsync("SELECT listened_ms FROM analytics.play_history WHERE id = @s", ("s", session)), 0, 15_000);
    }

    [Fact]
    public async Task Anonymous_listeners_are_deduplicated_by_client_id_and_cannot_play_private_tracks()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, durationMs: 4_000);
        var privateTrack = await _content.ReadyTrackAsync(artist, visibility: "Private", durationMs: 4_000);
        using var anonymous = _api.CreateApiClient();
        var clientId = Guid.NewGuid();

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var session = Guid.NewGuid();
            await SendAsync(anonymous, clientId, Event(session, track, "started", 0), Event(session, track, "completed", 4_000));
        }

        Assert.Equal((2L, 1L), await StatsAsync(track));

        var hidden = Guid.NewGuid();
        Assert.Equal(0, await SendAsync(anonymous, clientId, Event(hidden, privateTrack, "started", 0)));
        using var stats = await anonymous.GetAsync($"/api/v1/tracks/{privateTrack}/stats");
        Assert.Equal(HttpStatusCode.NotFound, stats.StatusCode);
    }

    [Fact]
    public async Task Invalid_batches_are_rejected()
    {
        using var anonymous = _api.CreateApiClient();
        var track = Guid.NewGuid();

        using (var empty = await anonymous.PostAsync("/api/v1/plays", new { events = Array.Empty<object>() }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal("play_batch_invalid", await empty.ProblemCodeAsync());
        }

        using (var tooMany = await anonymous.PostAsync("/api/v1/plays", new { events = Enumerable.Range(0, 51).Select(_ => Event(Guid.NewGuid(), track, "started", 0)).ToArray() }))
        {
            Assert.Equal("play_batch_invalid", await tooMany.ProblemCodeAsync());
        }

        using (var badType = await anonymous.PostAsync("/api/v1/plays", new { events = new[] { Event(Guid.NewGuid(), track, "skipped", 0) } }))
        {
            Assert.Equal("play_event_invalid", await badType.ProblemCodeAsync());
        }

        using var negative = await anonymous.PostAsync("/api/v1/plays", new { events = new[] { Event(Guid.NewGuid(), track, "progress", -1) } });
        Assert.Equal("play_event_invalid", await negative.ProblemCodeAsync());
    }

    private static object Event(Guid session, Guid track, string type, int listenedMs) =>
        new { id = Guid.NewGuid(), sessionId = session, trackId = track, type, positionMs = listenedMs, listenedMs, clientTimestamp = DateTimeOffset.UtcNow };

    /// <summary>Sends one batch; returns how many events were accepted.</summary>
    private static async Task<int> SendAsync(ApiClient client, Guid? clientId, params object[] events)
    {
        using var response = await client.PostAsync("/api/v1/plays", new { clientId, events });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.DataAsync()).GetProperty("accepted").GetInt32();
    }

    private async Task<(long Plays, long Listens)> StatsAsync(Guid track)
    {
        using var anonymous = _api.CreateApiClient();
        JsonElement stats = await anonymous.GetDataAsync($"/api/v1/tracks/{track}/stats");
        return (stats.GetProperty("plays").GetInt64(), stats.GetProperty("listens").GetInt64());
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
