using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Catalog;

/// <summary>Postgres FTS + trigram search (ADR-0010) on a real database.</summary>
public sealed class SearchTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public SearchTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    /// <summary>A word no other test uses, so results from the shared database don't interfere.</summary>
    private static string UniqueWord() => "qz" + new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(6).ToArray());

    [Fact]
    public async Task Finds_tracks_without_accents_and_while_typing_but_only_listed_ones()
    {
        var word = UniqueWord();
        var (artist, _, _) = await _content.ArtistAsync();
        var listed = await _content.ReadyTrackAsync(artist, $"Đêm Trăng Hà Nội {word}");
        await _content.ReadyTrackAsync(artist, $"Đêm Trăng Riêng Tư {word}", visibility: "Private");
        await _content.ReadyTrackAsync(artist, $"Đêm Trăng Không Liệt Kê {word}", visibility: "Unlisted");
        await TestContent.DraftTrackAsync(artist, $"Đêm Trăng Nháp {word}");
        using var anonymous = _api.CreateApiClient();

        Assert.Equal([listed], (await anonymous.GetDataAsync($"/api/v1/search/tracks?q={Uri.EscapeDataString($"dem trang ha noi {word}")}")).Ids());
        Assert.Equal([listed], (await anonymous.GetDataAsync($"/api/v1/search/tracks?q={Uri.EscapeDataString($"Đêm {word[..5]}")}")).Ids()); // prefix

        var overview = await anonymous.GetDataAsync($"/api/v1/search?q={word}");
        Assert.Equal([listed], overview.GetProperty("tracks").GetProperty("items").Ids());

        // The owner doesn't get their private tracks in search either (search is for listed content).
        Assert.Equal([listed], (await artist.GetDataAsync($"/api/v1/search/tracks?q={word}")).Ids());
    }

    [Fact]
    public async Task Taken_down_tracks_disappear_from_search()
    {
        var word = UniqueWord();
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, $"Takedown {word}");
        using var anonymous = _api.CreateApiClient();
        Assert.Equal([track], (await anonymous.GetDataAsync($"/api/v1/search/tracks?q={word}")).Ids());

        await _content.ExecuteAsync("UPDATE catalog.tracks SET taken_down_at = now(), takedown_reason = 'copyright' WHERE id = @id", ("id", track));

        Assert.Empty((await anonymous.GetDataAsync($"/api/v1/search/tracks?q={word}")).EnumerateArray());
    }

    [Fact]
    public async Task Empty_or_blank_query_returns_nothing_rather_than_everything()
    {
        using var anonymous = _api.CreateApiClient();
        using var response = await anonymous.GetAsync("/api/v1/search/tracks?q=%20%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.DataAsync()).EnumerateArray());
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
