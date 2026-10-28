using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Catalog;

/// <summary>Tracks and albums: private content is invisible to others, and only the owner can change it (IDOR → 404).</summary>
public sealed class CatalogAuthorizationTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public CatalogAuthorizationTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    [Fact]
    public async Task Private_track_is_invisible_to_others_and_only_the_owner_can_edit_or_delete_tracks()
    {
        var (owner, _, _) = await _content.ArtistAsync();
        var privateTrack = await _content.ReadyTrackAsync(owner, "Secret demo", "Private");
        var publicTrack = await _content.ReadyTrackAsync(owner, "Open song");
        var (other, _, _) = await _content.ArtistAsync(); // an artist too: the role alone grants nothing on others' tracks
        using var anonymous = _api.CreateApiClient();

        foreach (var client in new[] { other, anonymous })
        {
            using var get = await client.GetAsync($"/api/v1/tracks/{privateTrack}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        }

        Assert.Equal("Secret demo", (await owner.GetDataAsync($"/api/v1/tracks/{privateTrack}")).GetProperty("title").GetString());

        foreach (var track in new[] { privateTrack, publicTrack })
        {
            using var update = await other.PatchAsync($"/api/v1/tracks/{track}", new { title = "Hijacked", visibility = "Public" });
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            using var delete = await other.DeleteAsync($"/api/v1/tracks/{track}");
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }

        using (var anonymousUpdate = await anonymous.PatchAsync($"/api/v1/tracks/{publicTrack}", new { title = "x", visibility = "Public" }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousUpdate.StatusCode);
        }

        Assert.Equal("Open song", (await anonymous.GetDataAsync($"/api/v1/tracks/{publicTrack}")).GetProperty("title").GetString());

        using (var update = await owner.PatchAsync($"/api/v1/tracks/{publicTrack}", new { title = "Open song (remaster)", visibility = "Public" }))
        {
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }

        using (var delete = await owner.DeleteAsync($"/api/v1/tracks/{privateTrack}"))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }
    }

    [Fact]
    public async Task Private_album_is_invisible_to_others_and_only_the_owner_can_change_it()
    {
        var (owner, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(owner, "Album cut");
        var privateAlbum = await CreateAlbumAsync(owner, "Unreleased", "Private");
        var publicAlbum = await CreateAlbumAsync(owner, "Released", "Public");
        var (other, _, _) = await _content.ArtistAsync();
        var othersTrack = await _content.ReadyTrackAsync(other, "Not yours");
        using var anonymous = _api.CreateApiClient();

        foreach (var client in new[] { other, anonymous })
        {
            using var get = await client.GetAsync($"/api/v1/albums/{privateAlbum}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        }

        foreach (var album in new[] { privateAlbum, publicAlbum })
        {
            using var update = await other.PatchAsync($"/api/v1/albums/{album}", new { title = "Hijacked", kind = "Album", visibility = "Public" });
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            using var tracks = await other.SendAsync(HttpMethod.Put, $"/api/v1/albums/{album}/tracks", new { trackIds = new[] { othersTrack } });
            Assert.Equal(HttpStatusCode.NotFound, tracks.StatusCode);
            using var delete = await other.DeleteAsync($"/api/v1/albums/{album}");
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }

        // The owner's tracklist only takes the owner's tracks.
        using (var foreign = await owner.SendAsync(HttpMethod.Put, $"/api/v1/albums/{publicAlbum}/tracks", new { trackIds = new[] { track, othersTrack } }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
            Assert.Equal("album_tracks_invalid", await foreign.ProblemCodeAsync());
        }

        using (var set = await owner.SendAsync(HttpMethod.Put, $"/api/v1/albums/{publicAlbum}/tracks", new { trackIds = new[] { track } }))
        {
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        }

        var detail = await anonymous.GetDataAsync($"/api/v1/albums/{publicAlbum}");
        Assert.Equal("Released", detail.GetProperty("album").GetProperty("title").GetString());
        Assert.Equal([track], detail.GetProperty("tracks").Ids());
        Assert.Equal("Unreleased", (await owner.GetDataAsync($"/api/v1/albums/{privateAlbum}")).GetProperty("album").GetProperty("title").GetString());
    }

    private static async Task<Guid> CreateAlbumAsync(ApiClient client, string title, string visibility)
    {
        using var response = await client.PostAsync("/api/v1/albums", new { title, kind = "Album", visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
