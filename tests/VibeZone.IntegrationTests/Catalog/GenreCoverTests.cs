using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Catalog;

public sealed class GenreCoverTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public GenreCoverTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    [Fact]
    public async Task Genre_lists_covers_of_its_three_newest_listed_tracks_with_artwork()
    {
        var (artist, _, _) = await _content.ArtistAsync();

        // Published "in the future" so these are the genre's newest tracks whatever other tests add to the database.
        async Task<Guid> TrackAsync(int minutes, bool artwork = true, string visibility = "Public", bool takenDown = false)
        {
            var id = await _content.ReadyTrackAsync(artist, $"Cover {minutes}", visibility);
            await _content.ExecuteAsync(
                """
                UPDATE catalog.tracks
                SET genre_id = (SELECT id FROM catalog.genres WHERE slug = 'reggae'),
                    published_at = now() + interval '30 days' + make_interval(mins => @minutes),
                    artwork_id = CASE WHEN @artwork THEN gen_random_uuid() END,
                    taken_down_at = CASE WHEN @takenDown THEN now() END
                WHERE id = @id
                """,
                ("id", id), ("minutes", minutes), ("artwork", artwork), ("takenDown", takenDown));
            return id;
        }

        var oldest = await TrackAsync(1);
        var second = await TrackAsync(2);
        var third = await TrackAsync(3);
        await TrackAsync(4, artwork: false);
        await TrackAsync(5, visibility: "Private");
        await TrackAsync(6, visibility: "Unlisted");
        await TrackAsync(7, takenDown: true);
        var newest = await TrackAsync(8);

        using var anonymous = _api.CreateApiClient();
        var reggae = (await anonymous.GetDataAsync("/api/v1/genres")).EnumerateArray().Single(g => g.GetProperty("slug").GetString() == "reggae");
        var covers = reggae.GetProperty("covers").EnumerateArray().Select(c => c.GetString()!).ToList();

        Assert.Equal(3, covers.Count);
        Assert.Equal([newest, third, second], covers.Select(c => Guid.Parse(c.Split('/')[4])));
        Assert.All(covers, c => Assert.Contains("/artwork/small?v=", c, StringComparison.Ordinal));
        Assert.DoesNotContain(covers, c => c.Contains(oldest.ToString(), StringComparison.Ordinal));

        // Genres without listed artwork have an empty list, not null.
        Assert.All((await anonymous.GetDataAsync("/api/v1/genres")).EnumerateArray(), g => Assert.Equal(System.Text.Json.JsonValueKind.Array, g.GetProperty("covers").ValueKind));
    }

    public ValueTask DisposeAsync() => _api.DisposeAsync();
}
