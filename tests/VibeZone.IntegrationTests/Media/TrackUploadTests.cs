using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

/// <summary>One-request track creation (metadata + audio + cover) and cover management.</summary>
[Collection(MediaTests.Name)]
public sealed class TrackUploadTests(MediaFixture media, MailpitFixture mail)
{
    private const string Path = "/api/v1/track-uploads";

    [Fact]
    public async Task Creates_the_track_with_cover_and_audio_in_one_request()
    {
        using var artist = await media.UserAsync();

        using var response = await artist.SendAsync(HttpMethod.Post, Path, configure: r => r.Content = Form(
            title: "  One   request ", visibility: "Unlisted", description: "Made in one go", genreSlug: "pop",
            audio: TestAudio.Wav(TimeSpan.FromSeconds(3)), artwork: TestImages.Cover(900, 700)));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var status = await response.DataAsync();
        var trackId = status.GetProperty("trackId").GetGuid();
        Assert.Equal($"/api/v1/tracks/{trackId}", response.Headers.Location?.OriginalString);
        Assert.Equal("song.wav", status.GetProperty("fileName").GetString());

        var track = await artist.WaitForTrackAsync(trackId, "Ready", "Failed");
        Assert.Equal("Ready", track.GetProperty("status").GetString());
        Assert.Equal("One request", track.GetProperty("title").GetString());
        Assert.Equal("Unlisted", track.GetProperty("visibility").GetString());
        Assert.Equal("Made in one go", track.GetProperty("description").GetString());
        Assert.Equal("pop", track.GetProperty("genre").GetProperty("slug").GetString());

        // The cover survives audio processing (the worker only replaces audio assets).
        var artwork = track.GetProperty("artwork");
        Assert.Equal((320, 320), TestImages.Size(await GetImageAsync(artist, artwork.GetProperty("small").GetString()!)));
        Assert.Equal((700, 700), TestImages.Size(await GetImageAsync(artist, artwork.GetProperty("large").GetString()!)));
    }

    [Fact]
    public async Task Works_with_cookie_authentication_and_csrf_like_the_web_app()
    {
        using var browser = media.Api.CreateApiClient();
        await TestUsers.SignedInAsync(browser, mail);
        using (var becomeArtist = await browser.PostAsync("/api/v1/me/artist", new { name = "Cookie Artist", slug = $"c-{Guid.NewGuid():N}"[..20] }))
        {
            Assert.Equal(HttpStatusCode.Created, becomeArtist.StatusCode);
        }

        var csrf = await browser.FetchCsrfAsync();
        using var withoutToken = await browser.Http.PostAsync(Path, Form(audio: TestAudio.Wav(TimeSpan.FromSeconds(2))), AppUser.Ct);
        Assert.Equal("csrf_invalid", await withoutToken.ProblemCodeAsync());

        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = Form(audio: TestAudio.Wav(TimeSpan.FromSeconds(2)), artwork: TestImages.Cover(format: SkiaSharp.SKEncodedImageFormat.Jpeg)) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        using var response = await browser.Http.SendAsync(request, AppUser.Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var trackId = (await response.DataAsync()).GetProperty("trackId").GetGuid();
        var track = await browser.GetDataAsync($"/api/v1/tracks/{trackId}");
        using var image = await browser.GetAsync(track.GetProperty("artwork").GetProperty("small").GetString()!);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("", "track_title_invalid", "title")]
    [InlineData("no-audio", "audio_required", "audio")]
    [InlineData("renamed-audio", "unsupported_file_type", "audio")]
    [InlineData("text-artwork", "artwork_invalid", "artwork")]
    [InlineData("tiny-artwork", "artwork_too_small", "artwork")]
    [InlineData("bad-visibility", "track_visibility_invalid", "visibility")]
    public async Task Rejects_invalid_input_and_creates_nothing(string scenario, string code, string field)
    {
        using var artist = await media.UserAsync();
        var wav = TestAudio.Wav(TimeSpan.FromSeconds(2));
        var form = scenario switch
        {
            "" => Form(title: " ", audio: wav),
            "no-audio" => Form(audio: null),
            "renamed-audio" => Form(audio: TestAudio.RenamedPng()),
            "text-artwork" => Form(audio: wav, artwork: "<svg><script>alert(1)</script></svg>"u8.ToArray()),
            "tiny-artwork" => Form(audio: wav, artwork: TestImages.Cover(120, 120)),
            _ => Form(audio: wav, visibility: "Everyone"),
        };

        using var response = await artist.SendAsync(HttpMethod.Post, Path, configure: r => r.Content = form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
        var problem = await response.JsonAsync();
        Assert.Equal(code, problem.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.Equal(0, (await MyTracksAsync(artist)).GetArrayLength());
    }

    [Fact]
    public async Task Rejects_non_artists_anonymous_users_and_non_multipart_bodies()
    {
        using var listener = await media.UserAsync(artist: false);
        using var artist = await media.UserAsync();
        using var anonymous = media.Anonymous();

        using var notArtist = await listener.SendAsync(HttpMethod.Post, Path, configure: r => r.Content = Form(audio: TestAudio.Wav(TimeSpan.FromSeconds(1))));
        using var json = await artist.SendAsync(HttpMethod.Post, Path, new { title = "x", visibility = "Public" });
        using var noAuth = await anonymous.PostAsync(Path, Form(audio: TestAudio.Wav(TimeSpan.FromSeconds(1))), AppUser.Ct);

        Assert.Equal(HttpStatusCode.Forbidden, notArtist.StatusCode);
        Assert.Equal("artist_required", await notArtist.ProblemCodeAsync());
        Assert.Equal("multipart_required", await json.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, noAuth.StatusCode);
    }

    [Fact]
    public async Task Oversized_cover_is_rejected()
    {
        await using var api = new ApiFactory(media.Postgres.ConnectionString, new Dictionary<string, string>(media.Storage.Settings) { ["Media:MaxArtworkBytes"] = "20000" }, mail);
        using var artist = await media.UserAsync(api: api);

        using var response = await artist.SendAsync(HttpMethod.Post, Path, configure: r => r.Content = Form(audio: TestAudio.Wav(TimeSpan.FromSeconds(1)), artwork: new byte[30_000]));

        Assert.Equal("artwork_too_large", await response.ProblemCodeAsync());
        Assert.Equal(0, (await MyTracksAsync(artist)).GetArrayLength());
    }

    [Fact]
    public async Task Cover_of_a_private_track_is_only_visible_to_its_owner()
    {
        using var owner = await media.UserAsync();
        using var other = await media.UserAsync();
        using var anonymous = media.Anonymous();
        using var response = await owner.SendAsync(HttpMethod.Post, Path, configure: r => r.Content = Form(visibility: "Private", audio: TestAudio.Wav(TimeSpan.FromSeconds(1)), artwork: TestImages.Cover()));
        var trackId = (await response.DataAsync()).GetProperty("trackId").GetGuid();
        var url = (await owner.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("artwork").GetProperty("small").GetString()!;

        using var asOwner = await owner.SendAsync(HttpMethod.Get, url);
        using var asOther = await other.SendAsync(HttpMethod.Get, url);
        using var asAnonymous = await anonymous.GetAsync(url, AppUser.Ct);

        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asAnonymous.StatusCode);
    }

    [Fact]
    public async Task Owner_can_replace_and_remove_the_cover_of_an_existing_track()
    {
        using var owner = await media.UserAsync();
        using var other = await media.UserAsync();
        var trackId = await owner.CreateTrackAsync();
        var path = $"/api/v1/tracks/{trackId}/artwork";
        Assert.Equal(JsonValueKind.Null, (await owner.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("artwork").ValueKind);

        using var first = await owner.SendAsync(HttpMethod.Put, path, configure: r => r.Content = new ByteArrayContent(TestImages.Cover(format: SkiaSharp.SKEncodedImageFormat.Webp)));
        using var second = await owner.SendAsync(HttpMethod.Put, path, configure: r => r.Content = new ByteArrayContent(TestImages.Cover(400, 400)));
        using var byOther = await other.SendAsync(HttpMethod.Put, path, configure: r => r.Content = new ByteArrayContent(TestImages.Cover()));
        using var invalid = await owner.SendAsync(HttpMethod.Put, path, configure: r => r.Content = new ByteArrayContent("not an image"u8.ToArray()));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstLarge = (await first.DataAsync()).GetProperty("large").GetString();
        var secondLarge = (await second.DataAsync()).GetProperty("large").GetString()!;
        Assert.NotEqual(firstLarge, secondLarge); // a new version is a new URL
        Assert.Equal(secondLarge, (await owner.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("artwork").GetProperty("large").GetString());
        Assert.Equal((400, 400), TestImages.Size(await GetImageAsync(owner, secondLarge)));
        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal("artwork_invalid", await invalid.ProblemCodeAsync());

        using var removed = await owner.SendAsync(HttpMethod.Delete, path);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await owner.GetDataAsync($"/api/v1/tracks/{trackId}")).GetProperty("artwork").ValueKind);
        using var gone = await owner.SendAsync(HttpMethod.Get, secondLarge);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    private static MultipartFormDataContent Form(byte[]? audio, string title = "Uploaded track", string visibility = "Public", string? description = null, string? genreSlug = null, byte[]? artwork = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(visibility), "visibility" },
        };
        if (description is not null)
        {
            form.Add(new StringContent(description), "description");
        }

        if (genreSlug is not null)
        {
            form.Add(new StringContent(genreSlug), "genreSlug");
        }

        if (artwork is not null)
        {
            var part = new ByteArrayContent(artwork);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(part, "artwork", "cover.png");
        }

        if (audio is not null)
        {
            var part = new ByteArrayContent(audio);
            part.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(part, "audio", "song.wav");
        }

        return form;
    }

    private static async Task<byte[]> GetImageAsync(AppUser user, string url)
    {
        using var response = await user.SendAsync(HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsByteArrayAsync(AppUser.Ct);
    }

    private static async Task<JsonElement> MyTracksAsync(AppUser user) => await user.GetDataAsync("/api/v1/me/tracks");
}
