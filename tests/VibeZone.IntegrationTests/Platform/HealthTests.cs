using System.Net;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Platform;

public sealed class HealthTests(PostgresFixture postgres, MinioFixture storage) : IClassFixture<MinioFixture>
{
    [Fact]
    public async Task Ready_is_healthy_with_migrated_database()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, storage.Settings);
        var (status, body) = await GetAsync(api, "/health/ready");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("storage").GetString());
    }

    [Fact]
    public async Task Ready_is_unhealthy_when_migrations_are_pending()
    {
        var emptyDb = await postgres.CreateEmptyDatabaseAsync($"pending_{Guid.NewGuid():N}");
        await using var api = new ApiFactory(emptyDb);

        var (status, body) = await GetAsync(api, "/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_is_unhealthy_and_live_is_healthy_when_database_is_unreachable()
    {
        // Port 1 on loopback: nothing listens, connection is refused immediately.
        await using var api = new ApiFactory("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");

        var (readyStatus, readyBody) = await GetAsync(api, "/health/ready");
        var (liveStatus, _) = await GetAsync(api, "/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyStatus);
        // No exception text or connection details leak into the probe response.
        Assert.DoesNotContain("Port", readyBody.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", readyBody.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, liveStatus);
    }

    [Fact]
    public void Startup_fails_fast_without_connection_string()
    {
        using var api = new ApiFactory(connectionString: null);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("ConnectionStrings:Database", error.ToString(), StringComparison.Ordinal);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(ApiFactory api, string path)
    {
        using var client = api.CreateClient();
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(json).RootElement.Clone());
    }
}
