using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Identity;

public sealed class CsrfTests(PostgresFixture postgres, MailpitFixture mail) : IAsyncDisposable
{
    private readonly ApiFactory _api = new(postgres.ConnectionString, mailpit: mail);

    [Fact]
    public async Task Unsafe_requests_without_a_token_are_rejected_before_doing_anything()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);

        // Login acts on the cookie (login CSRF), so it keeps requiring the token.
        using var response = await client.SendAsync(HttpMethod.Post, "/api/v1/auth/login",
            new { user.Email, user.Password, rememberMe = false }, withCsrf: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_invalid", await response.ProblemCodeAsync());
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], c => c.Contains("vz_session=", StringComparison.Ordinal));
        using var me = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/resend-verification")]
    [InlineData("/api/v1/auth/forgot-password")]
    public async Task Account_endpoints_that_never_touch_the_cookie_work_without_csrf(string path)
    {
        using var client = _api.CreateApiClient();
        var user = TestUsers.New();

        using var response = await client.SendAsync(HttpMethod.Post, path,
            new { user.Email, user.Handle, user.Password, displayName = "Native app", locale = "en" }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task A_token_without_its_cookie_is_rejected()
    {
        using var attacker = _api.CreateApiClient();
        var stolenToken = await attacker.FetchCsrfAsync();
        using var victim = _api.CreateApiClient(); // different cookie jar: no matching antiforgery cookie

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-CSRF-TOKEN", stolenToken);
        using var response = await victim.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Anonymous_token_is_not_accepted_after_signing_in()
    {
        using var client = _api.CreateApiClient();
        var user = await TestUsers.RegisterVerifiedAsync(client, mail);
        var anonymousToken = await client.FetchCsrfAsync();
        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(user.Email, user.Password));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/sessions/revoke-others");
        request.Headers.Add("X-CSRF-TOKEN", anonymousToken);
        using var response = await client.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("csrf_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Csrf_endpoint_is_not_cacheable_and_sets_a_strict_host_cookie()
    {
        using var client = _api.CreateApiClient();

        using var response = await client.GetAsync("/api/v1/auth/csrf");

        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-vz_af=", StringComparison.Ordinal)).ToLowerInvariant();
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
