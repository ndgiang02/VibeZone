using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class PasswordTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    [Fact]
    public async Task Forgot_password_for_unknown_email_is_accepted_and_sends_nothing()
    {
        using var client = _api.CreateApiClient();
        var stranger = TestUsers.New().Email;

        using var response = await client.PostAsync("/api/v1/auth/forgot-password", new { email = stranger, locale = "en" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Task.Delay(1500, TestContext.Current.CancellationToken); // give a (wrongly) queued email time to arrive
        Assert.Empty(await mail.SearchAsync(stranger));
    }

    [Fact]
    public async Task Reset_password_signs_out_everywhere_and_the_link_works_once()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(client, mail);
        using var otherDevice = _api.CreateApiClient();
        Assert.Equal(HttpStatusCode.NoContent, await otherDevice.LoginAsync(user.Email, user.Password));

        using var forgot = await client.PostAsync("/api/v1/auth/forgot-password", new { email = user.Email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var email = await mail.WaitForMessageAsync(user.Email, "Reset your VibeZone password");
        Assert.Equal(user.Email, email.Query("email"));

        const string newPassword = "a brand new passphrase";
        var body = new { email = email.Query("email"), token = email.Query("token"), newPassword };
        using var reset = await client.PostAsync("/api/v1/auth/reset-password", body);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // Existing sessions on both devices are revoked.
        using (var me = await client.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }
        using (var me = await otherDevice.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }

        Assert.Equal(HttpStatusCode.Unauthorized, await client.LoginAsync(user.Email, user.Password));
        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(user.Email, newPassword));
        await mail.WaitForMessageAsync(user.Email, "password was changed");

        using var reuse = await client.PostAsync("/api/v1/auth/reset-password", body);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal("token_invalid", await reuse.ProblemCodeAsync());
    }

    [Fact]
    public async Task Reset_with_unknown_email_looks_like_a_bad_token()
    {
        using var client = _api.CreateApiClient();

        using var response = await client.PostAsync("/api/v1/auth/reset-password", new { email = TestUsers.New().Email, token = "abc", newPassword = "a brand new passphrase" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("token_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Change_password_requires_the_current_one_and_revokes_other_sessions_only()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(client, mail);
        using var otherDevice = _api.CreateApiClient();
        Assert.Equal(HttpStatusCode.NoContent, await otherDevice.LoginAsync(user.Email, user.Password));

        using var wrong = await client.PostAsync("/api/v1/me/password", new { currentPassword = "not my password", newPassword = "another long passphrase" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal("current_password_incorrect", await wrong.ProblemCodeAsync());

        using var changed = await client.PostAsync("/api/v1/me/password", new { currentPassword = user.Password, newPassword = "another long passphrase" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using (var me = await client.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.OK, me.StatusCode); }
        using (var me = await otherDevice.GetAsync("/api/v1/me")) { Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode); }
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
