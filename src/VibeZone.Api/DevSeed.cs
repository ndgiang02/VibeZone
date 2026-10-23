using VibeZone.Modules.Catalog.Application.Tracks;
using VibeZone.Modules.Catalog.Domain;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Playlists.Application;
using VibeZone.Modules.Playlists.Domain;

namespace VibeZone.Api;

/// <summary>
/// DEVELOPMENT ONLY sample content: <c>dotnet run --project src/VibeZone.Api -- seed-dev</c>.
/// Creates three confirmed demo accounts (password <see cref="Password"/>), their artist profiles, Draft
/// tracks (no audio: nothing becomes Ready or playable), an album and a playlist — through the same
/// application services as the API, so every rule applies. Idempotent: existing demo accounts are skipped.
/// Never runs outside the Development environment; nothing here is in a migration.
/// </summary>
internal static class DevSeed
{
    public const string Command = "seed-dev";
    public const string Password = "dev-password-123";

    private static readonly (string Handle, string Name, string Genre, string[] Tracks)[] Artists =
    [
        ("dev_lan_anh", "Lan Anh (demo)", "pop", ["Mưa tháng sáu (demo)", "Hà Nội đêm (demo)", "Gió mùa (demo)"]),
        ("dev_minh_khoa", "Minh Khoa (demo)", "electronic", ["Neon Drive (demo)", "Saigon Lights (demo)"]),
        ("dev_the_lofi_cat", "The Lofi Cat (demo)", "ambient", ["Rainy Window (demo)", "Late Night Study (demo)", "Coffee Steam (demo)"]),
    ];

    public static async Task<int> RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.Logger.LogError("{Command} only runs in the Development environment.", Command);
            return 1;
        }

        var ct = CancellationToken.None;
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accounts = services.GetRequiredService<IUserAccounts>();
        var artists = services.GetRequiredService<ArtistService>();
        var tracks = services.GetRequiredService<TrackService>();
        var albums = services.GetRequiredService<AlbumService>();
        var playlists = services.GetRequiredService<PlaylistService>();

        foreach (var (handle, name, genre, titles) in Artists)
        {
            var email = $"{handle.Replace('_', '.')}@example.test";
            if (await accounts.FindByEmailAsync(email, ct) is not null)
            {
                app.Logger.LogInformation("Demo account {Email} exists; skipped.", email);
                continue;
            }

            var created = await accounts.CreateAsync(new NewAccount(email, handle, Password, name, "vi"), ct);
            if (!created.IsSuccess)
            {
                app.Logger.LogError("Could not create {Email}: {Code}", email, created.Error!.Code);
                return 1;
            }

            var userId = created.Value.Id;
            await accounts.ConfirmEmailAsync(userId, await accounts.CreateEmailConfirmationTokenAsync(userId, ct), ct);
            await artists.CreateForUserAsync(userId, new CreateArtistRequest("Hồ sơ mẫu cho môi trường phát triển (demo)."), ct);

            var trackIds = new List<Guid>();
            foreach (var title in titles)
            {
                var track = await tracks.CreateAsync(userId, new SaveTrackRequest(title, "Bài mẫu, chưa có audio (demo).", genre, TrackVisibility.Public), ct);
                trackIds.Add(track.Value.Id);
            }

            var album = await albums.CreateAsync(userId, new SaveAlbumRequest($"{name} — Demo EP", AlbumKind.Ep, "Album mẫu (demo).", DateOnly.FromDateTime(DateTime.UtcNow), TrackVisibility.Public), ct);
            await albums.SetTracksAsync(userId, album.Value.Id, new SetAlbumTracksRequest(trackIds), ct);

            var playlist = await playlists.CreateAsync(userId, new SavePlaylistRequest($"Playlist của {name}", "Playlist mẫu (demo).", PlaylistVisibility.Public), ct);
            foreach (var trackId in trackIds)
            {
                await playlists.AddTrackAsync(userId, playlist.Value.Id, trackId, ct);
            }

            app.Logger.LogInformation("Seeded {Email} with {Count} draft tracks, an album and a playlist.", email, trackIds.Count);
        }

        app.Logger.LogInformation("Dev seed done. Sign in with any demo account and password {Password}.", Password);
        return 0;
    }
}
