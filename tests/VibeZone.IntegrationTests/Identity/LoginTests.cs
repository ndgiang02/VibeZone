using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class LoginTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    [Fact]
    public async Task Unknown_email_and_wrong_password_are_indistinguishable()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        using var wrongPassword = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = "definitely wrong password" });
        using var unknownEmail = await client.PostAsync("/api/v1/auth/login", new { email = TestUsers.New().Email, password = "definitely wrong password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var a = await wrongPassword.JsonAsync();
        var b = await unknownEmail.JsonAsync();
        Assert.Equal("invalid_credentials", a.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("code").GetString(), b.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("title").GetString(), b.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Unverified_account_needs_confirmation_only_after_the_right_password()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterAsync(client);

        using var wrong = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = "definitely wrong password" });
        using var right = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });

        Assert.Equal("invalid_credentials", await wrong.ProblemCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        Assert.Equal("email_not_confirmed", await right.ProblemCodeAsync());
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_correct_password()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await client.LoginAsync(user.Email, "wrong password " + i));
        }

        using var locked = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("invalid_credentials", await locked.ProblemCodeAsync());
    }

    [Fact]
    public async Task Session_cookie_is_host_prefixed_secure_httponly_lax()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        using var login = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-vz_session=", StringComparison.Ordinal)).ToLowerInvariant();
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("domain=", cookie, StringComparison.Ordinal);
        Assert.DoesNotContain("expires=", cookie, StringComparison.Ordinal); // not "remember me": browser-session cookie
    }

    [Fact]
    public async Task Plain_http_local_mode_issues_unprefixed_non_secure_cookies_and_still_works()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string> { ["Security:SecureCookies"] = "false" }, mail);
        using var client = new ApiClient(api.CreateClient(new() { BaseAddress = new Uri("http://localhost"), HandleCookies = true }));
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        using var login = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password });

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("vz_session=", StringComparison.Ordinal)).ToLowerInvariant();
        Assert.DoesNotContain("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        using var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Secure_mode_refuses_to_issue_a_csrf_cookie_over_plain_http()
    {
        // Guards the default: without HTTPS (or a trusted TLS proxy) the API must not silently downgrade.
        using var client = _api.CreateClient(new() { BaseAddress = new Uri("http://localhost") });

        using var response = await client.GetAsync("/api/v1/auth/csrf", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Remember_me_issues_a_persistent_cookie()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        using var login = await client.PostAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password, rememberMe = true });

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-vz_session=", StringComparison.Ordinal));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_me_is_401_problem()
    {
        using var client = _api.CreateApiClient();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_ip()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string> { ["RateLimiting:AuthPermitsPerMinute"] = "3" }, mail);
        using var client = api.CreateApiClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await client.LoginAsync("nobody@example.test", "wrong password!!"));
        }

        using var limited = await client.PostAsync("/api/v1/auth/login", new { email = "nobody@example.test", password = "wrong password!!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
