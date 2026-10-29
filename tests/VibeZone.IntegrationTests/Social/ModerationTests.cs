using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.IntegrationTests.Infrastructure;
using VibeZone.Modules.Identity.Application;

namespace VibeZone.IntegrationTests.Social;

public sealed class ModerationTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public ModerationTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    public static TheoryData<string, string> StaffEndpoints => new()
    {
        { "GET", "/api/v1/moderation/reports" },
        { "POST", $"/api/v1/moderation/reports/{Guid.NewGuid()}/resolve" },
        { "POST", $"/api/v1/moderation/content/Track/{Guid.NewGuid()}/restore" },
        { "GET", "/api/v1/admin/tracks" },
        { "GET", "/api/v1/admin/audit-log" },
        { "GET", "/api/v1/admin/users" },
        { "POST", "/api/v1/admin/users" },
        { "PUT", $"/api/v1/admin/users/{Guid.NewGuid()}/roles/Moderator" },
        { "DELETE", $"/api/v1/admin/users/{Guid.NewGuid()}/roles/Moderator" },
        { "GET", "/api/v1/admin/jobs" },
        { "GET", "/api/v1/admin/jobs/summary" },
        { "GET", "/api/v1/admin/outbox" },
    };

    /// <summary>Endpoints a moderator may use; everything else in <see cref="StaffEndpoints"/> is admin-only.</summary>
    private static readonly string[] ModeratorPrefixes = ["/api/v1/moderation/", "/api/v1/admin/tracks"];

    [Theory]
    [MemberData(nameof(StaffEndpoints))]
    public async Task Staff_endpoints_reject_anonymous_users_and_people_without_the_role(string method, string path)
    {
        using var anonymous = _api.CreateApiClient();
        using (var response = await anonymous.SendAsync(new HttpMethod(method), path, method == "POST" ? new { action = "dismiss" } : null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var (user, _) = await _content.UserAsync();
        using (var response = await user.SendAsync(new HttpMethod(method), path, method == "POST" ? new { action = "dismiss" } : null))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var moderatorAllowed = ModeratorPrefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal));
        if (!moderatorAllowed)
        {
            var (moderator, _) = await _content.StaffAsync("Moderator");
            using var response = await moderator.SendAsync(new HttpMethod(method), path, null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Reported_track_taken_down_by_a_moderator_becomes_unplayable_and_is_audited()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, "Stolen beat");
        var (reporter, _) = await _content.UserAsync();
        var (second, _) = await _content.UserAsync();

        var report = await ReportAsync(reporter, "Track", track, "Copyright");
        await ReportAsync(second, "Track", track, "Spam");
        using (var again = await reporter.PostAsync("/api/v1/reports", new { subjectType = "Track", subjectId = track, reason = "Abuse" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal("report_exists", await again.ProblemCodeAsync());
        }

        using (var own = await artist.PostAsync("/api/v1/reports", new { subjectType = "Track", subjectId = track, reason = "Other" }))
        {
            Assert.Equal("report_own_content", await own.ProblemCodeAsync());
        }

        var (moderator, _) = await _content.StaffAsync("Moderator");
        var queue = await moderator.GetDataAsync("/api/v1/moderation/reports?status=Open");
        Assert.Contains(report, queue.Ids());

        using (var resolved = await moderator.PostAsync($"/api/v1/moderation/reports/{report}/resolve", new { action = "remove", note = "Confirmed" }))
        {
            Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
            Assert.Equal("Actioned", (await resolved.DataAsync()).GetProperty("status").GetString());
        }

        using (var twice = await moderator.PostAsync($"/api/v1/moderation/reports/{report}/resolve", new { action = "dismiss" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        }

        // Every open report on the track was resolved with it.
        Assert.DoesNotContain(track, (await moderator.GetDataAsync("/api/v1/moderation/reports?status=Open")).EnumerateArray()
            .Select(r => r.GetProperty("subjectId").GetGuid()));

        // Gone for everyone but its owner (who can see why) and moderators; nobody can play it except moderators.
        using var anonymous = _api.CreateApiClient();
        await AssertHiddenAsync(anonymous, track);
        await AssertHiddenAsync(reporter, track);
        Assert.True((await artist.GetDataAsync($"/api/v1/tracks/{track}")).GetProperty("takenDown").GetBoolean());
        Assert.DoesNotContain(track, (await anonymous.GetDataAsync("/api/v1/tracks")).Ids());
        Assert.Equal(0, await PlayAsync(reporter, track));
        Assert.Equal(0, await PlayAsync(artist, track));
        using (var like = await reporter.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null))
        {
            Assert.Equal(HttpStatusCode.NotFound, like.StatusCode);
        }

        // Audited; reporters and the owner are told.
        var (admin, _) = await _content.StaffAsync("Admin");
        var audit = (await admin.GetDataAsync("/api/v1/admin/audit-log")).EnumerateArray()
            .Single(e => e.GetProperty("subjectId").GetString() == track.ToString());
        Assert.Equal("content_removed", audit.GetProperty("action").GetString());
        await TestContent.EventuallyAsync(async () => await HasNotificationAsync(artist, "ContentRemoved"), "the owner is told");
        await TestContent.EventuallyAsync(async () => await HasNotificationAsync(reporter, "ReportResolved"), "the reporter is told");
        await TestContent.EventuallyAsync(async () => await HasNotificationAsync(second, "ReportResolved"), "the second reporter is told");

        // Restore brings it back (also audited).
        using (var restored = await moderator.PostAsync($"/api/v1/moderation/content/Track/{track}/restore"))
        {
            Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        }

        Assert.Equal(track, (await anonymous.GetDataAsync($"/api/v1/tracks/{track}")).GetProperty("id").GetGuid());
        Assert.Contains((await admin.GetDataAsync("/api/v1/admin/audit-log")).EnumerateArray(),
            e => e.GetProperty("action").GetString() == "content_restored" && e.GetProperty("subjectId").GetString() == track.ToString());
    }

    [Fact]
    public async Task Dismissed_report_leaves_the_content_up()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist);
        var (reporter, _) = await _content.UserAsync();
        var report = await ReportAsync(reporter, "Track", track, "Other");
        var (moderator, _) = await _content.StaffAsync("Moderator");

        using (var invalid = await moderator.PostAsync($"/api/v1/moderation/reports/{report}/resolve", new { action = "ban" }))
        {
            Assert.Equal("report_action_invalid", await invalid.ProblemCodeAsync());
        }

        using (var dismissed = await moderator.PostAsync($"/api/v1/moderation/reports/{report}/resolve", new { action = "dismiss" }))
        {
            Assert.Equal("Rejected", (await dismissed.DataAsync()).GetProperty("status").GetString());
        }

        using var anonymous = _api.CreateApiClient();
        Assert.Equal(track, (await anonymous.GetDataAsync($"/api/v1/tracks/{track}")).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cannot_report_content_you_cannot_see()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var privateTrack = await _content.ReadyTrackAsync(artist, visibility: "Private");
        var (reporter, _) = await _content.UserAsync();

        foreach (var (type, id) in new[] { ("Track", privateTrack), ("Comment", Guid.NewGuid()), ("Playlist", Guid.NewGuid()) })
        {
            using var response = await reporter.PostAsync("/api/v1/reports", new { subjectType = type, subjectId = id, reason = "Spam" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("report_subject_not_found", await response.ProblemCodeAsync());
        }
    }

    [Fact]
    public async Task Admin_grants_and_revokes_roles_with_an_audit_trail_but_cannot_drop_their_own_admin()
    {
        var (admin, _) = await _content.StaffAsync("Admin");
        var (user, _) = await _content.UserAsync();
        var userId = (await user.GetDataAsync("/api/v1/me")).GetProperty("id").GetGuid();
        var adminId = (await admin.GetDataAsync("/api/v1/me")).GetProperty("id").GetGuid();

        using (var forbidden = await user.GetAsync("/api/v1/moderation/reports")) { Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); }

        using (var granted = await admin.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{userId}/roles/Moderator", null))
        {
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        }

        // Applies to the user's existing session right away.
        using (var allowed = await user.GetAsync("/api/v1/moderation/reports")) { Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); }

        using (var artist = await admin.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{userId}/roles/Artist", null))
        {
            Assert.Equal("role_not_assignable", await artist.ProblemCodeAsync());
        }

        using (var self = await admin.DeleteAsync($"/api/v1/admin/users/{adminId}/roles/Admin"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
            Assert.Equal("role_self_admin", await self.ProblemCodeAsync());
        }

        using (var missing = await admin.SendAsync(HttpMethod.Put, $"/api/v1/admin/users/{Guid.NewGuid()}/roles/Moderator", null))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        using (var revoked = await admin.DeleteAsync($"/api/v1/admin/users/{userId}/roles/Moderator"))
        {
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        }

        using (var forbiddenAgain = await user.GetAsync("/api/v1/moderation/reports")) { Assert.Equal(HttpStatusCode.Forbidden, forbiddenAgain.StatusCode); }

        var actions = (await admin.GetDataAsync("/api/v1/admin/audit-log")).EnumerateArray()
            .Where(e => e.GetProperty("subjectId").GetString() == userId.ToString())
            .Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Equal(["role_revoked", "role_granted"], actions);
    }

    [Fact]
    public async Task Command_line_grant_bootstraps_the_first_admin_and_is_audited()
    {
        var (client, user) = await _content.UserAsync();
        using (var forbidden = await client.GetAsync("/api/v1/admin/users")) { Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); }

        await using (var scope = _api.Services.CreateAsyncScope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
            var admin = scope.ServiceProvider.GetRequiredService<AdminUserService>();
            var ct = TestContext.Current.CancellationToken;

            Assert.Equal("role_not_assignable", (await admin.GrantFromCommandLineAsync(await accounts.FindByEmailAsync(user.Email, ct), "Artist", ct)).Error?.Code);
            Assert.Equal("user_not_found", (await admin.GrantFromCommandLineAsync(await accounts.FindByEmailAsync("nobody@example.test", ct), "Admin", ct)).Error?.Code);
            for (var i = 0; i < 2; i++) // idempotent
            {
                var granted = await admin.GrantFromCommandLineAsync(await accounts.FindByEmailAsync(user.Email.ToUpperInvariant(), ct), "admin", ct);
                Assert.Contains("Admin", granted.Value.Roles);
            }
        }

        // The existing session gets the role at once; one audit entry, marked as coming from the command line.
        var userId = (await client.GetDataAsync("/api/v1/me")).GetProperty("id").GetGuid();
        var entry = Assert.Single((await client.GetDataAsync("/api/v1/admin/audit-log")).EnumerateArray(),
            e => e.GetProperty("subjectId").GetString() == userId.ToString());
        Assert.Equal("role_granted", entry.GetProperty("action").GetString());
        Assert.Equal("cli", entry.GetProperty("metadata").GetProperty("via").GetString());
    }

    [Fact]
    public async Task Command_line_grant_requires_a_confirmed_email()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);
        await using var scope = _api.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
        var ct = TestContext.Current.CancellationToken;

        var result = await scope.ServiceProvider.GetRequiredService<AdminUserService>()
            .GrantFromCommandLineAsync(await accounts.FindByEmailAsync(user.Email, ct), "Admin", ct);

        Assert.Equal("user_not_found", result.Error?.Code);
    }

    private static async Task<Guid> ReportAsync(ApiClient client, string subjectType, Guid subjectId, string reason)
    {
        using var response = await client.PostAsync("/api/v1/reports", new { subjectType, subjectId, reason, details = "Test report" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    private static async Task AssertHiddenAsync(ApiClient client, Guid track)
    {
        foreach (var path in new[] { $"/api/v1/tracks/{track}", $"/api/v1/tracks/{track}/stream", $"/api/v1/tracks/{track}/playback", $"/api/v1/tracks/{track}/comments" })
        {
            using var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path} → {(int)response.StatusCode}");
        }
    }

    private static async Task<int> PlayAsync(ApiClient client, Guid track)
    {
        var session = Guid.NewGuid();
        using var response = await client.PostAsync("/api/v1/plays", new
        {
            events = new[] { new { id = Guid.NewGuid(), sessionId = session, trackId = track, type = "started", positionMs = 0, listenedMs = 0 } },
        });
        return (await response.DataAsync()).GetProperty("accepted").GetInt32();
    }

    private static async Task<bool> HasNotificationAsync(ApiClient client, string type) =>
        (await client.GetDataAsync("/api/v1/me/notifications")).EnumerateArray().Any(n => n.GetProperty("type").GetString() == type);

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
