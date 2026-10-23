using System.Net;
using Microsoft.EntityFrameworkCore;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class RegistrationTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    [Fact]
    public async Task Register_verify_login_me_logout_round_trip()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client, locale: "en");

        var email = await mail.WaitForMessageAsync(user.Email, "Confirm your VibeZone email");
        Assert.StartsWith($"{ApiFactory.PublicBaseUrl}/en/verify-email?", email.Link.ToString(), StringComparison.Ordinal);

        using var verify = await client.PostAsync("/api/v1/auth/verify-email", new { userId = Guid.Parse(email.Query("userId")), token = email.Query("token") });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(user.Email, user.Password));
        var me = await client.GetDataAsync("/api/v1/me");
        Assert.Equal(user.Handle, me.GetProperty("handle").GetString());
        Assert.Equal(user.Email, me.GetProperty("email").GetString());
        Assert.Equal("Test User", me.GetProperty("displayName").GetString());
        Assert.Equal("en", me.GetProperty("locale").GetString());
        Assert.True(me.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(0, me.GetProperty("roles").GetArrayLength());

        using var logout = await client.PostAsync("/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var afterLogout = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Vietnamese_registration_gets_a_vietnamese_email_and_link()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client, locale: "vi");

        var email = await mail.WaitForMessageAsync(user.Email, "Xác nhận email VibeZone");

        Assert.Contains("Chào mừng bạn đến với VibeZone", email.Text, StringComparison.Ordinal);
        Assert.Contains("/vi/verify-email?", email.Link.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registering_an_existing_email_looks_identical_and_only_notifies_the_owner()
    {
        using var client = _api.CreateApiClient();
        var owner = await TestUsers.RegisterVerifiedAsync(client, mail);

        using var first = await client.PostAsync("/api/v1/auth/register", new { email = "fresh-" + owner.Email, handle = "f" + owner.Handle[1..], password = TestUsers.Password, locale = "en" });
        using var duplicate = await client.PostAsync("/api/v1/auth/register", new { email = owner.Email, handle = "d" + owner.Handle[1..], password = "another password 123", locale = "en" });

        Assert.Equal(first.StatusCode, duplicate.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await mail.WaitForMessageAsync(owner.Email, "You already have a VibeZone account");

        await using var db = PostgresFixture.CreateDbContext(postgres.ConnectionString);
        var count = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM identity.users WHERE normalized_email = {owner.Email.ToUpperInvariant()}").SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("not-an-email", "validhandle1", "a long enough password", "email", "email_invalid")]
    [InlineData("ok@example.test", "ab", "a long enough password", "handle", "handle_invalid")]
    [InlineData("ok@example.test", "Bad Handle!", "a long enough password", "handle", "handle_invalid")]
    [InlineData("ok@example.test", "admin", "a long enough password", "handle", "handle_reserved")]
    [InlineData("ok@example.test", "validhandle2", "short", "password", "password_too_short")]
    public async Task Invalid_input_returns_field_level_error_codes(string email, string handle, string password, string field, string code)
    {
        using var client = _api.CreateApiClient();

        using var response = await client.PostAsync("/api/v1/auth/register", new { email, handle, password, locale = "en" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.JsonAsync();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(code, problem.GetProperty("errors").GetProperty(field)[0].GetString());
    }

    [Fact]
    public async Task Taken_handle_is_a_conflict()
    {
        using var client = _api.CreateApiClient();
        var existing = await TestUsers.RegisterAsync(client);

        using var response = await client.PostAsync("/api/v1/auth/register", new { email = TestUsers.New().Email, handle = existing.Handle.ToUpperInvariant(), password = TestUsers.Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("handle_taken", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Verification_with_a_tampered_token_fails()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);
        var email = await mail.WaitForMessageAsync(user.Email, "Confirm");

        using var response = await client.PostAsync("/api/v1/auth/verify-email", new { userId = Guid.Parse(email.Query("userId")), token = email.Query("token") + "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("token_invalid", await response.ProblemCodeAsync());
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
