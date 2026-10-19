using System.Net;
using System.Text.Json;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Platform;

public sealed class HttpPipelineTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Responses_carry_security_headers()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync("/api/v1/genres", TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cross_origin_requests_get_no_cors_grant()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/genres");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_with_trace_id()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Global_rate_limit_returns_429_problem_details()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, new Dictionary<string, string>
        {
            ["RateLimiting:GlobalPermitsPerMinute"] = "3",
        });
        using var client = api.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var ok = await client.GetAsync("/api/v1/genres", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var limited = await client.GetAsync("/api/v1/genres", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);

        // Health probes are exempt so orchestrators never see 429.
        using var health = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
