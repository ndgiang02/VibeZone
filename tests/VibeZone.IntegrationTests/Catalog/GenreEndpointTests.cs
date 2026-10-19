using System.Net;
using System.Net.Http.Json;
using VibeZone.IntegrationTests.Infrastructure;
using VibeZone.Modules.Catalog.Endpoints;

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
        var body = await response.Content.ReadFromJsonAsync<GenreListResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(23, body.Items.Count);
        Assert.Contains(body.Items, g => g.Slug == "v-pop" && g.Name == "V-Pop");
        Assert.Equal(body.Items.Select(g => g.Name).Order(StringComparer.Ordinal), body.Items.Select(g => g.Name));
    }
}
