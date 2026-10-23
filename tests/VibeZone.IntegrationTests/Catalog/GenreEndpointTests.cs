using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Catalog;

public sealed class GenreEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Lists_reference_genres_from_the_database_ordered_by_name()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync("/api/v1/genres", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        var body = await response.JsonAsync();
        var names = body.GetProperty("data").EnumerateArray().Select(g => g.GetProperty("name").GetString()).ToList();
        Assert.Equal(23, names.Count);
        Assert.Contains(body.GetProperty("data").EnumerateArray(), g => g.GetProperty("slug").GetString() == "v-pop" && g.GetProperty("name").GetString() == "V-Pop");
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        var meta = body.GetProperty("meta");
        Assert.Equal(1, meta.GetProperty("page").GetInt32());
        Assert.Equal(23, meta.GetProperty("total").GetInt32());
        Assert.False(meta.GetProperty("hasMore").GetBoolean());
    }
}
