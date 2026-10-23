using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class SessionAndProfileTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    [Fact]
    public async Task Sessions_are_listed_and_revoking_one_signs_that_device_out()
    {
        using var laptop = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(laptop, mail);
        using var phone = _api.CreateApiClient();
        phone.Http.DefaultRequestHeaders.UserAgent.ParseAdd("PhoneBrowser/1.0");
        Assert.Equal(HttpStatusCode.NoContent, await phone.LoginAsync(user.Email, user.Password));

        var sessions = (await laptop.GetDataAsync("/api/v1/me/sessions")).EnumerateArray().ToList();
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.GetProperty("isCurrent").GetBoolean());
        var phoneSession = Assert.Single(sessions, s => s.GetProperty("userAgent").GetString() == "PhoneBrowser/1.0");

        using var revoke = await laptop.DeleteAsync($"/api/v1/me/sessions/{phoneSession.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using (var me = await phone.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }
        using (var me = await laptop.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.OK, me.StatusCode); }
    }

    [Fact]
    public async Task Cannot_revoke_someone_elses_session()
    {
        using var alice = _api.CreateApiClient();
        await TestUsers.SignedInAsync(alice, mail);
        using var bob = _api.CreateApiClient();
        await TestUsers.SignedInAsync(bob, mail);
        var bobSession = (await bob.GetDataAsync("/api/v1/me/sessions"))[0].GetProperty("id").GetGuid();

        using var attempt = await alice.DeleteAsync($"/api/v1/me/sessions/{bobSession}");

        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
        using var bobStillIn = await bob.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, bobStillIn.StatusCode);
    }

    [Fact]
    public async Task Revoke_others_keeps_only_the_current_session()
    {
        using var a = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(a, mail);
        using var b = _api.CreateApiClient();
        using var c = _api.CreateApiClient();
        await b.LoginAsync(user.Email, user.Password);
        await c.LoginAsync(user.Email, user.Password);

        using var response = await a.PostAsync("/api/v1/me/sessions/revoke-others");

        Assert.Equal(2, (await response.DataAsync()).GetProperty("revoked").GetInt32());
        Assert.Single((await a.GetDataAsync("/api/v1/me/sessions")).EnumerateArray());
        using (var me = await b.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }
        using (var me = await c.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }
    }

    [Fact]
    public async Task Profile_update_changes_display_name_and_language()
    {
        using var client = _api.CreateApiClient();
        await TestUsers.SignedInAsync(client, mail);

        using var updated = await client.PatchAsync("/api/v1/me/profile", new { displayName = "  Nguyễn   Văn A ", locale = "vi" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var me = await updated.DataAsync();
        Assert.Equal("Nguyễn Văn A", me.GetProperty("displayName").GetString());
        Assert.Equal("vi", me.GetProperty("locale").GetString());

        using var invalid = await client.PatchAsync("/api/v1/me/profile", new { displayName = "Ok", locale = "fr" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("locale_unsupported", await invalid.ProblemCodeAsync());
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
