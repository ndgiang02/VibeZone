using System.Net;
using Npgsql;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>
/// Catalog content and staff accounts for tests that don't exercise the media pipeline. Tracks and artists are
/// created through the public API; only the parts that would need the worker (Ready status) or an admin who
/// doesn't exist yet (the first role grant) are written to the database, by the test.
/// </summary>
public sealed class TestContent(ApiFactory api, string connectionString, MailpitFixture mail)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Settings for an API host that runs many social calls from one test-server IP.</summary>
    public static readonly IReadOnlyDictionary<string, string> RelaxedRateLimits = new Dictionary<string, string>
    {
        ["RateLimiting:GlobalPermitsPerMinute"] = "100000",
        ["RateLimiting:SocialPermitsPerMinute"] = "100000",
        ["RateLimiting:PlayBatchesPerMinute"] = "100000",
    };

    /// <summary>A signed-in browser user (cookie session).</summary>
    public async Task<(ApiClient Client, TestUser User)> UserAsync()
    {
        var client = api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(client, mail);
        return (client, user);
    }

    /// <summary>A signed-in user with an artist profile; returns the artist id.</summary>
    public async Task<(ApiClient Client, TestUser User, Guid ArtistId)> ArtistAsync()
    {
        var (client, user) = await UserAsync();
        using var created = await client.PostAsync("/api/v1/me/artist", new { bio = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (client, user, (await created.DataAsync()).GetProperty("id").GetGuid());
    }

    /// <summary>A signed-in user holding <paramref name="role"/> (granted in the database before signing in).</summary>
    public async Task<(ApiClient Client, TestUser User)> StaffAsync(string role)
    {
        var client = api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);
        await GrantRoleAsync(user.Email, role);
        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(user.Email, user.Password));
        return (client, user);
    }

    /// <summary>Creates a track via the API, then marks it Ready (as the worker would) with the given duration.</summary>
    public async Task<Guid> ReadyTrackAsync(ApiClient artist, string title = "Test track", string visibility = "Public", int durationMs = 180_000)
    {
        var trackId = await DraftTrackAsync(artist, title, visibility);
        await ExecuteAsync(
            "UPDATE catalog.tracks SET status = 'ready', duration_ms = @duration, published_at = now() WHERE id = @id",
            ("id", trackId), ("duration", durationMs));
        return trackId;
    }

    public static async Task<Guid> DraftTrackAsync(ApiClient artist, string title = "Draft track", string visibility = "Public")
    {
        using var response = await artist.PostAsync("/api/v1/tracks", new { title, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    public Task GrantRoleAsync(string email, string role) =>
        ExecuteAsync(
            """
            INSERT INTO identity.user_roles (user_id, role_id)
            SELECT u.id, r.id FROM identity.users u, identity.roles r
            WHERE u.normalized_email = upper(@email) AND r.normalized_name = upper(@role)
            ON CONFLICT DO NOTHING
            """,
            ("email", email), ("role", role));

    public async Task<long> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = Command(connection, sql, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Runs a statement that must affect exactly one row.</summary>
    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = Command(connection, sql, parameters);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    /// <summary>Polls until <paramref name="condition"/> holds (outbox delivery is asynchronous, ~1 s).</summary>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, string because, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {because}.");
            await Task.Delay(250, Ct);
        }
    }
}
