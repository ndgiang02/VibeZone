using System.Net;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

/// <summary>The artist profile is the account's public face (ADR-0013): one name, one URL, one picture.</summary>
[Collection(MediaTests.Name)]
public sealed class ArtistIdentityTests(MediaFixture media)
{
    [Fact]
    public async Task Artist_profile_takes_the_account_handle_name_and_picture_and_follows_their_changes()
    {
        using var user = await media.UserAsync(artist: false);
        using (var picture = await user.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", configure: r => r.Content = new ByteArrayContent(TestImages.Cover(400, 400))))
        {
            Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        }

        // Becoming an artist asks for nothing the account already has; slug/name in the body are ignored.
        using var created = await user.SendAsync(HttpMethod.Post, "/api/v1/me/artist", new { bio = "Hello", name = "Someone else", slug = "someone-else" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var me = await user.GetDataAsync("/api/v1/me");
        var handle = me.GetProperty("handle").GetString()!;
        var artist = await created.DataAsync();
        Assert.Equal(handle, artist.GetProperty("slug").GetString());
        Assert.Equal(me.GetProperty("displayName").GetString(), artist.GetProperty("name").GetString());
        Assert.Equal("Hello", artist.GetProperty("bio").GetString());
        Assert.Equal(me.GetProperty("avatar").GetProperty("large").GetString(), artist.GetProperty("avatar").GetProperty("large").GetString());

        // Renaming the account renames the artist everywhere (profile, public page, tracks).
        using (var renamed = await user.SendAsync(HttpMethod.Patch, "/api/v1/me/profile", new { displayName = "Renamed Artist" }))
        {
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        }

        var trackId = await user.CreateTrackAsync();
        using var anonymous = media.Anonymous();
        var page = await GetDataAsync(anonymous, $"/api/v1/artists/{handle}");
        Assert.Equal("Renamed Artist", page.GetProperty("artist").GetProperty("name").GetString());
        Assert.Equal("Renamed Artist", (await user.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("artist").GetProperty("name").GetString());

        // A new account picture is the artist picture; removing it removes both.
        string newLarge;
        using (var replaced = await user.SendAsync(HttpMethod.Put, "/api/v1/me/avatar", configure: r => r.Content = new ByteArrayContent(TestImages.Cover(500, 500))))
        {
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
            newLarge = (await replaced.DataAsync()).GetProperty("large").GetString()!;
        }

        Assert.Equal(newLarge, (await user.GetDataAsync("/api/v1/me/artist")).GetProperty("avatar").GetProperty("large").GetString());
        using (var picture = await anonymous.GetAsync(newLarge, AppUser.Ct))
        {
            Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        }

        using (var removed = await user.SendAsync(HttpMethod.Delete, "/api/v1/me/avatar"))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Equal(JsonValueKind.Null, (await user.GetDataAsync("/api/v1/me/artist")).GetProperty("avatar").ValueKind);
    }

    [Fact]
    public async Task Artist_has_no_picture_of_its_own_but_keeps_its_banner()
    {
        using var artist = await media.UserAsync();
        using var avatar = await artist.SendAsync(HttpMethod.Put, "/api/v1/me/artist/avatar", configure: r => r.Content = new ByteArrayContent(TestImages.Cover(400, 400)));
        Assert.True(avatar.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"Unexpected {avatar.StatusCode}");

        using var banner = await artist.SendAsync(HttpMethod.Put, "/api/v1/me/artist/banner", configure: r => r.Content = new ByteArrayContent(TestImages.Cover(1200, 400)));
        Assert.Equal(HttpStatusCode.OK, banner.StatusCode);
        var large = (await banner.DataAsync()).GetProperty("large").GetString()!;
        Assert.Equal(large, (await artist.GetDataAsync("/api/v1/me/artist")).GetProperty("banner").GetProperty("large").GetString());
        using var anonymous = media.Anonymous();
        using (var image = await anonymous.GetAsync(large, AppUser.Ct))
        {
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        }

        using var removed = await artist.SendAsync(HttpMethod.Delete, "/api/v1/me/artist/banner");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await artist.GetDataAsync("/api/v1/me/artist")).GetProperty("banner").ValueKind);
    }

    [Fact]
    public async Task Becoming_an_artist_twice_is_rejected()
    {
        using var artist = await media.UserAsync();
        using var again = await artist.SendAsync(HttpMethod.Post, "/api/v1/me/artist", new { bio = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("artist_exists", await again.ProblemCodeAsync());
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, AppUser.Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.DataAsync();
    }
}
