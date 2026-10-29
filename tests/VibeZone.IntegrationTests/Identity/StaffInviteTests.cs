using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class StaffInviteTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;
    private readonly MailpitFixture _mail;

    public StaffInviteTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
        _mail = mail;
    }

    [Fact]
    public async Task Invited_staff_member_chooses_a_password_from_the_email_and_signs_in_with_the_role()
    {
        var (admin, _) = await _content.StaffAsync("Admin");
        var invitee = TestUsers.New("staff");

        using var created = await admin.PostAsync("/api/v1/admin/users", new { invitee.Email, invitee.Handle, displayName = "New Mod", role = "moderator", locale = "en" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.DataAsync();
        var userId = dto.GetProperty("id").GetGuid();
        Assert.Equal(["Moderator"], dto.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(dto.GetProperty("emailConfirmed").GetBoolean());

        // No password yet: the account can't sign in with anything until the invitation is accepted.
        using var invitee1 = _api.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, await invitee1.LoginAsync(invitee.Email, invitee.Password));

        var email = await _mail.WaitForMessageAsync(invitee.Email, "invited");
        Assert.Contains("Moderator", email.Text, StringComparison.Ordinal);
        Assert.Contains("/en/accept-invite", email.Link.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(userId, Guid.Parse(email.Query("userId")));
        var token = email.Query("token");

        using (var wrong = await invitee1.PostAsync("/api/v1/auth/accept-invite", new { userId, token = token + "x", invitee.Password }))
        {
            Assert.Equal("token_invalid", await wrong.ProblemCodeAsync());
        }

        using (var weak = await invitee1.PostAsync("/api/v1/auth/accept-invite", new { userId, token, password = "short" }))
        {
            Assert.Equal("password_too_short", await weak.ProblemCodeAsync());
        }

        using (var accepted = await invitee1.PostAsync("/api/v1/auth/accept-invite", new { userId, token, invitee.Password }))
        {
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        }

        // Single use: the same link can't set the password again.
        using (var reused = await invitee1.PostAsync("/api/v1/auth/accept-invite", new { userId, token, password = "another long password" }))
        {
            Assert.Equal("token_invalid", await reused.ProblemCodeAsync());
        }

        Assert.Equal(HttpStatusCode.NoContent, await invitee1.LoginAsync(invitee.Email, invitee.Password));
        var me = await invitee1.GetDataAsync("/api/v1/me");
        Assert.Contains("Moderator", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        using (var queue = await invitee1.GetAsync("/api/v1/moderation/reports")) { Assert.Equal(HttpStatusCode.OK, queue.StatusCode); }

        var entry = Assert.Single((await admin.GetDataAsync("/api/v1/admin/audit-log")).EnumerateArray(),
            e => e.GetProperty("subjectId").GetString() == userId.ToString());
        Assert.Equal("user_invited", entry.GetProperty("action").GetString());
        Assert.Equal("Moderator", entry.GetProperty("metadata").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Invitation_is_validated_and_never_takes_over_an_existing_account()
    {
        var (admin, _) = await _content.StaffAsync("Admin");
        var (_, existing) = await _content.UserAsync();
        var fresh = TestUsers.New("staff");

        using (var taken = await admin.PostAsync("/api/v1/admin/users", new { existing.Email, handle = fresh.Handle, role = "Admin" }))
        {
            Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
            Assert.Equal("email_taken", await taken.ProblemCodeAsync());
        }

        using (var handle = await admin.PostAsync("/api/v1/admin/users", new { fresh.Email, existing.Handle, role = "Admin" }))
        {
            Assert.Equal("handle_taken", await handle.ProblemCodeAsync());
        }

        using (var role = await admin.PostAsync("/api/v1/admin/users", new { fresh.Email, fresh.Handle, role = "Artist" }))
        {
            Assert.Equal("role_not_assignable", await role.ProblemCodeAsync());
        }

        using (var email = await admin.PostAsync("/api/v1/admin/users", new { email = "not-an-email", fresh.Handle, role = "Admin" }))
        {
            Assert.Equal("email_invalid", await email.ProblemCodeAsync());
        }

        // Nothing was created by the rejected attempts; the existing account's password still works.
        Assert.Empty((await admin.GetDataAsync($"/api/v1/admin/users?q={fresh.Handle}")).EnumerateArray());
        using var client = _api.CreateApiClient();
        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(existing.Email, existing.Password));
    }

    [Fact]
    public async Task Accepting_requires_a_real_invitation()
    {
        // A self-registered account already has a password, so no invitation can (re)set it.
        var (client, user) = await _content.UserAsync();
        var userId = (await client.GetDataAsync("/api/v1/me")).GetProperty("id").GetGuid();

        using var unknown = await client.PostAsync("/api/v1/auth/accept-invite", new { userId = Guid.NewGuid(), token = "abc", user.Password });
        Assert.Equal("token_invalid", await unknown.ProblemCodeAsync());
        using var existing = await client.PostAsync("/api/v1/auth/accept-invite", new { userId, token = "abc", user.Password });
        Assert.Equal("token_invalid", await existing.ProblemCodeAsync());
    }

    public ValueTask DisposeAsync() => _api.DisposeAsync();
}
