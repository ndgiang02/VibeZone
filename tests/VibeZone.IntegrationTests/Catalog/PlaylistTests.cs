using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Catalog;

public sealed class PlaylistTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public PlaylistTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    [Fact]
    public async Task Create_add_reorder_and_reload_keeps_the_new_order()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var a = await _content.ReadyTrackAsync(artist, "Alpha");
        var b = await _content.ReadyTrackAsync(artist, "Bravo");
        var c = await _content.ReadyTrackAsync(artist, "Charlie");
        var (listener, _) = await _content.UserAsync();

        var playlist = await CreateAsync(listener, "Road trip", "Public");
        foreach (var track in new[] { a, b, c })
        {
            using var added = await listener.PostAsync($"/api/v1/playlists/{playlist}/tracks", new { trackId = track });
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        }

        // Adding again is a no-op.
        using (var again = await listener.PostAsync($"/api/v1/playlists/{playlist}/tracks", new { trackId = a }))
        {
            Assert.Equal(3, (await again.DataAsync()).GetProperty("trackCount").GetInt32());
        }

        Assert.Equal([a, b, c], await TrackIdsAsync(listener, playlist));

        using (var reordered = await listener.SendAsync(HttpMethod.Put, $"/api/v1/playlists/{playlist}/tracks/order", new { trackIds = new[] { c, a, b } }))
        {
            Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        }

        // A fresh client (new session, nothing cached) sees the persisted order.
        using var anonymous = _api.CreateApiClient();
        Assert.Equal([c, a, b], await TrackIdsAsync(anonymous, playlist));

        using (var removed = await listener.DeleteAsync($"/api/v1/playlists/{playlist}/tracks/{a}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Equal([c, b], await TrackIdsAsync(listener, playlist));

        var mine = (await listener.GetDataAsync($"/api/v1/me/playlists?containsTrack={b}")).EnumerateArray().ToList();
        Assert.True(Assert.Single(mine).GetProperty("containsTrack").GetBoolean());
    }

    [Fact]
    public async Task Reorder_with_unknown_tracks_is_rejected()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var a = await _content.ReadyTrackAsync(artist);
        var playlist = await CreateAsync(artist, "Mine", "Public");
        using (await artist.PostAsync($"/api/v1/playlists/{playlist}/tracks", new { trackId = a })) { }

        using var response = await artist.SendAsync(HttpMethod.Put, $"/api/v1/playlists/{playlist}/tracks/order", new { trackIds = new[] { Guid.NewGuid(), a } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Private_playlist_is_invisible_to_others_and_only_the_owner_can_change_it()
    {
        var (owner, _) = await _content.UserAsync();
        var (other, _) = await _content.UserAsync();
        using var anonymous = _api.CreateApiClient();
        var playlist = await CreateAsync(owner, "Secret", "Private");

        foreach (var client in new[] { other, anonymous })
        {
            using var get = await client.GetAsync($"/api/v1/playlists/{playlist}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            using var tracks = await client.GetAsync($"/api/v1/playlists/{playlist}/tracks");
            Assert.Equal(HttpStatusCode.NotFound, tracks.StatusCode);
        }

        // Non-owner writes look exactly like a missing playlist (no existence leak), even on a public one.
        var publicPlaylist = await CreateAsync(owner, "Open", "Public");
        foreach (var id in new[] { playlist, publicPlaylist })
        {
            using var update = await other.PatchAsync($"/api/v1/playlists/{id}", new { title = "Hacked", visibility = "Public" });
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            using var add = await other.PostAsync($"/api/v1/playlists/{id}/tracks", new { trackId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
            using var reorder = await other.SendAsync(HttpMethod.Put, $"/api/v1/playlists/{id}/tracks/order", new { trackIds = Array.Empty<Guid>() });
            Assert.Equal(HttpStatusCode.NotFound, reorder.StatusCode);
            using var delete = await other.DeleteAsync($"/api/v1/playlists/{id}");
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }

        using var anonymousCreate = await anonymous.PostAsync("/api/v1/playlists", new { title = "x", visibility = "Public" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);

        Assert.Equal("Open", (await owner.GetDataAsync($"/api/v1/playlists/{publicPlaylist}")).GetProperty("title").GetString());
        Assert.Equal("Open", (await anonymous.GetDataAsync($"/api/v1/playlists/{publicPlaylist}")).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cannot_add_someone_elses_private_or_unready_track()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var privateTrack = await _content.ReadyTrackAsync(artist, visibility: "Private");
        var draft = await TestContent.DraftTrackAsync(artist);
        var (listener, _) = await _content.UserAsync();
        var playlist = await CreateAsync(listener, "List", "Public");

        foreach (var track in new[] { privateTrack, draft })
        {
            using var add = await listener.PostAsync($"/api/v1/playlists/{playlist}/tracks", new { trackId = track });
            Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
            Assert.Equal("track_not_found", await add.ProblemCodeAsync());
        }

        // The owner can put their own private track in their playlist; others still don't see it in the list.
        var ownList = await CreateAsync(artist, "Own", "Public");
        using (var add = await artist.PostAsync($"/api/v1/playlists/{ownList}/tracks", new { trackId = privateTrack }))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        Assert.Equal([privateTrack], await TrackIdsAsync(artist, ownList));
        Assert.Empty(await TrackIdsAsync(listener, ownList));
    }

    private static async Task<Guid> CreateAsync(ApiClient client, string title, string visibility)
    {
        using var response = await client.PostAsync("/api/v1/playlists", new { title, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> TrackIdsAsync(ApiClient client, Guid playlistId) =>
        (await client.GetDataAsync($"/api/v1/playlists/{playlistId}/tracks")).Ids();

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
