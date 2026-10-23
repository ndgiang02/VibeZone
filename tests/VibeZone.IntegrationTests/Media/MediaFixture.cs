using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Media;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaTests : ICollectionFixture<MediaFixture>
{
    public const string Name = "media";
}

/// <summary>
/// API + MinIO + the real worker container. Media tests run serially (one worker, and the large-file test
/// measures allocations), with users created through the public API and signed in with app tokens.
/// </summary>
public sealed class MediaFixture(PostgresFixture postgres, MailpitFixture mail) : IAsyncLifetime
{
    /// <summary>Short lease so a killed worker's job becomes claimable quickly; heartbeats extend it every 2 s.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromSeconds(6);

    public MinioFixture Storage { get; } = new();

    public ApiFactory Api { get; private set; } = null!;

    public IContainer Worker { get; private set; } = null!;

    public PostgresFixture Postgres => postgres;

    public async ValueTask InitializeAsync()
    {
        await Storage.InitializeAsync();
        Api = new ApiFactory(postgres.ConnectionString, Storage.Settings, mail);
        Worker = await MediaWorkerContainer.StartAsync(postgres.ConnectionString, Storage, Lease);
    }

    /// <summary>Kills the current worker (SIGKILL) and starts a fresh one.</summary>
    public async Task ReplaceWorkerAfterKillAsync()
    {
        await MediaWorkerContainer.KillAsync(Worker);
        await Worker.DisposeAsync();
        Worker = await MediaWorkerContainer.StartAsync(postgres.ConnectionString, Storage, Lease);
    }

    public async Task<AppUser> UserAsync(bool artist = true, ApiFactory? api = null)
    {
        api ??= Api;
        using var browser = api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(browser, mail);
        var http = api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        using var token = await http.PostAsJsonAsync("/api/v1/auth/token", new { user.Email, user.Password, deviceName = "Integration test" }, AppUser.Ct);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        var app = new AppUser(http, (await token.DataAsync()).GetProperty("accessToken").GetString()!);
        if (artist)
        {
            using var created = await app.SendAsync(HttpMethod.Post, "/api/v1/me/artist", new { bio = (string?)null });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        return app;
    }

    /// <summary>Anonymous client (no cookies, no token).</summary>
    public HttpClient Anonymous() => Api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    public async ValueTask DisposeAsync()
    {
        if (Worker is not null)
        {
            await Worker.DisposeAsync();
        }

        if (Api is not null)
        {
            await Api.DisposeAsync();
        }

        await Storage.DisposeAsync();
    }
}

/// <summary>A signed-in native-app client: every request carries <c>Authorization: Bearer</c>.</summary>
public sealed class AppUser(HttpClient http, string accessToken) : IDisposable
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HttpClient Http => http;

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        configure?.Invoke(request);
        var response = await http.SendAsync(request, Ct);
        await response.Content.LoadIntoBufferAsync(Ct);
        return response;
    }

    public async Task<JsonElement> GetDataAsync(string path)
    {
        using var response = await SendAsync(HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.DataAsync();
    }

    public async Task<Guid> CreateTrackAsync(string visibility = "Public", string title = "Test track")
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/v1/tracks", new { title, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    public async Task<HttpResponseMessage> UploadAsync(Guid trackId, byte[] body, long? declaredLength = null, string fileName = "song.wav")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tracks/{trackId}/upload") { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Upload-Length", (declaredLength ?? body.Length).ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
        var response = await http.SendAsync(request, Ct);
        await response.Content.LoadIntoBufferAsync(Ct);
        return response;
    }

    public async Task<JsonElement> UploadReadyAsync(byte[] wav, string visibility = "Public", TimeSpan? timeout = null)
    {
        var trackId = await CreateTrackAsync(visibility);
        using var upload = await UploadAsync(trackId, wav);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        var track = await WaitForTrackAsync(trackId, timeout ?? TimeSpan.FromMinutes(3), "Ready", "Failed");
        Assert.Equal("Ready", track.GetProperty("status").GetString());
        return track;
    }

    /// <summary>Polls the track (like the app does) until its status is one of <paramref name="statuses"/>.</summary>
    public Task<JsonElement> WaitForTrackAsync(Guid trackId, params string[] statuses) => WaitForTrackAsync(trackId, TimeSpan.FromMinutes(3), statuses);

    public async Task<JsonElement> WaitForTrackAsync(Guid trackId, TimeSpan timeout, params string[] statuses)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var track = await GetDataAsync($"/api/v1/tracks/{trackId}");
            if (statuses.Contains(track.GetProperty("status").GetString()) || DateTime.UtcNow > deadline)
            {
                return track;
            }

            await Task.Delay(500, Ct);
        }
    }

    public void Dispose() => http.Dispose();
}
