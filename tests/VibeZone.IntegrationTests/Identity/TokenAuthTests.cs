using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

/// <summary>Mobile/native token sign-in (ADR-0007): bearer access tokens + rotating refresh tokens bound to sessions.</summary>
public sealed class TokenAuthTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Token_sign_in_returns_a_pair_without_cookies_and_the_access_token_authenticates()
    {
        var user = await VerifiedUserAsync();
        using var app = MobileClient();

        using var response = await app.PostAsJsonAsync("/api/v1/auth/token", new { user.Email, user.Password, deviceName = "Pixel 8" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        var tokens = await response.JsonAsync();
        Assert.Equal("Bearer", tokens.GetProperty("tokenType").GetString());
        Assert.Equal(900, tokens.GetProperty("expiresIn").GetInt32());
        Assert.StartsWith("vza_", tokens.GetProperty("accessToken").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("vzr_", tokens.GetProperty("refreshToken").GetString(), StringComparison.Ordinal);

        using var me = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(tokens));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(user.Handle, (await me.JsonAsync()).GetProperty("handle").GetString());
    }

    [Fact]
    public async Task Bearer_requests_need_no_csrf_token()
    {
        var user = await VerifiedUserAsync();
        using var app = MobileClient();
        var tokens = await IssueAsync(app, user);

        using var update = await SendAsync(app, HttpMethod.Patch, "/api/v1/me/profile", Access(tokens), new { displayName = "From the app", locale = "vi" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("From the app", (await update.JsonAsync()).GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Token_sign_in_applies_the_same_credential_rules_as_cookie_login()
    {
        using var browser = _api.CreateApiClient();
        var unverified = await TestUsers.RegisterAsync(browser);
        using var app = MobileClient();

        using var wrong = await app.PostAsJsonAsync("/api/v1/auth/token", new { unverified.Email, password = "definitely wrong password" }, Ct);
        using var unknown = await app.PostAsJsonAsync("/api/v1/auth/token", new { TestUsers.New().Email, password = "definitely wrong password" }, Ct);
        using var notConfirmed = await app.PostAsJsonAsync("/api/v1/auth/token", new { unverified.Email, unverified.Password }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("invalid_credentials", await wrong.ProblemCodeAsync());
        Assert.Equal("invalid_credentials", await unknown.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, notConfirmed.StatusCode);
        Assert.Equal("email_not_confirmed", await notConfirmed.ProblemCodeAsync());
    }

    [Fact]
    public async Task Refresh_rotates_the_pair_and_reusing_an_old_refresh_token_revokes_the_session()
    {
        var user = await VerifiedUserAsync();
        using var app = MobileClient();
        var first = await IssueAsync(app, user);

        using var refreshed = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(first) }, Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = await refreshed.JsonAsync();
        Assert.NotEqual(Refresh(first), Refresh(second));
        Assert.NotEqual(Access(first), Access(second));
        using (var me = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(second)))
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        // The first refresh token was already exchanged: treat as stolen and end the whole session.
        using var replay = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(first) }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("invalid_refresh_token", await replay.ProblemCodeAsync());

        using var afterReplay = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(second) }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
        using var meAfter = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(second));
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("vzr_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Unknown_refresh_tokens_are_rejected(string refreshToken)
    {
        using var app = MobileClient();

        using var response = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_refresh_token", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Revoke_signs_the_token_session_out_and_is_idempotent()
    {
        var user = await VerifiedUserAsync();
        using var app = MobileClient();
        var tokens = await IssueAsync(app, user);

        using var revoke = await app.PostAsJsonAsync("/api/v1/auth/token/revoke", new { refreshToken = Refresh(tokens) }, Ct);
        using var again = await app.PostAsJsonAsync("/api/v1/auth/token/revoke", new { refreshToken = Refresh(tokens) }, Ct);
        using var me = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(tokens));
        using var refresh = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(tokens) }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Contains("invalid_token", me.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Bearer_logout_revokes_the_token_session()
    {
        var user = await VerifiedUserAsync();
        using var app = MobileClient();
        var tokens = await IssueAsync(app, user);

        using var logout = await SendAsync(app, HttpMethod.Post, "/api/v1/auth/logout", Access(tokens));
        using var refresh = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(tokens) }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Token_sessions_are_listed_and_can_be_revoked_from_the_web()
    {
        using var browser = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(browser, mail);
        using var app = MobileClient();
        var tokens = await IssueAsync(app, user, deviceName: "Pixel 8");

        var fromApp = (await (await SendAsync(app, HttpMethod.Get, "/api/v1/me/sessions", Access(tokens))).JsonAsync()).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, fromApp.Count);
        var phone = Assert.Single(fromApp, s => s.GetProperty("isCurrent").GetBoolean());
        Assert.Equal("Pixel 8", phone.GetProperty("userAgent").GetString());

        using var revoke = await browser.DeleteAsync($"/api/v1/me/sessions/{phone.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var me = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(tokens));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_on_the_web_signs_the_app_out()
    {
        using var browser = _api.CreateApiClient();
        var user = await TestUsers.SignedInAsync(browser, mail);
        using var app = MobileClient();
        var tokens = await IssueAsync(app, user);

        using var change = await browser.PostAsync("/api/v1/me/password", new { currentPassword = user.Password, newPassword = "a brand new long passphrase" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var me = await SendAsync(app, HttpMethod.Get, "/api/v1/me", Access(tokens));
        using var refresh = await app.PostAsJsonAsync("/api/v1/auth/token/refresh", new { refreshToken = Refresh(tokens) }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task An_invalid_bearer_token_never_falls_back_to_the_session_cookie()
    {
        using var browser = _api.CreateApiClient();
        await TestUsers.SignedInAsync(browser, mail);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "vza_forged");
        using var response = await browser.Http.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_requests_still_require_csrf()
    {
        using var browser = _api.CreateApiClient();
        await TestUsers.SignedInAsync(browser, mail);

        using var response = await browser.SendAsync(HttpMethod.Patch, "/api/v1/me/profile", new { displayName = "x", locale = "en" }, withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_invalid", await response.ProblemCodeAsync());
    }

    /// <summary>Registers and verifies exactly as the app does: no cookies, no CSRF token.</summary>
    private async Task<TestUser> VerifiedUserAsync()
    {
        using var app = MobileClient();
        var user = TestUsers.New("m");
        using var register = await app.PostAsJsonAsync("/api/v1/auth/register", new { user.Email, user.Handle, user.Password, displayName = "App User", locale = "en" }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var email = await mail.WaitForMessageAsync(user.Email, "Confirm");
        using var verify = await app.PostAsJsonAsync("/api/v1/auth/verify-email", new { userId = Guid.Parse(email.Query("userId")), token = email.Query("token") }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return user;
    }

    /// <summary>Like a Flutter app: no cookie jar, no CSRF handling.</summary>
    private HttpClient MobileClient()
    {
        var client = _api.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Dart/3.9 (dart:io)");
        return client;
    }

    private static async Task<JsonElement> IssueAsync(HttpClient app, TestUser user, string? deviceName = null)
    {
        using var response = await app.PostAsJsonAsync("/api/v1/auth/token", new { user.Email, user.Password, deviceName }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.JsonAsync();
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient app, HttpMethod method, string path, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await app.SendAsync(request, Ct);
        await response.Content.LoadIntoBufferAsync(Ct);
        return response;
    }

    private static string Access(JsonElement tokens) => tokens.GetProperty("accessToken").GetString()!;

    private static string Refresh(JsonElement tokens) => tokens.GetProperty("refreshToken").GetString()!;

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
