using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>Wraps HttpClient the way the web app talks to the API: cookies + X-CSRF-TOKEN on unsafe methods.</summary>
public sealed class ApiClient(HttpClient http) : IDisposable
{
    private string? _csrf;

    public HttpClient Http => http;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public Task<HttpResponseMessage> GetAsync(string path) => http.GetAsync(path, Ct);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PatchAsync(string path, object? body) => SendAsync(HttpMethod.Patch, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, bool withCsrf = true)
    {
        var response = await SendOnceAsync(method, path, body, withCsrf);
        await response.Content.LoadIntoBufferAsync(Ct); // so the caller can still read the body after we peek
        if (withCsrf && response.StatusCode == HttpStatusCode.BadRequest && await response.ProblemCodeAsync() == "csrf_invalid")
        {
            // Same recovery as the web app: the session changed server-side (e.g. revoked), so the
            // identity-bound token is stale. Fetch a fresh one and retry once.
            response.Dispose();
            _csrf = null;
            response = await SendOnceAsync(method, path, body, withCsrf);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, bool withCsrf)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        if (withCsrf)
        {
            request.Headers.Add("X-CSRF-TOKEN", _csrf ??= await FetchCsrfAsync());
        }

        var response = await http.SendAsync(request, Ct);
        if (path.StartsWith("/api/v1/auth/login", StringComparison.Ordinal) || path.StartsWith("/api/v1/auth/logout", StringComparison.Ordinal))
        {
            _csrf = null; // tokens are bound to the identity; the web app refetches after these too
        }

        return response;
    }

    public async Task<string> FetchCsrfAsync()
    {
        var response = await http.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        return response.GetProperty("token").GetString()!;
    }

    public async Task<HttpStatusCode> LoginAsync(string email, string password, bool rememberMe = false) =>
        (await PostAsync("/api/v1/auth/login", new { email, password, rememberMe })).StatusCode;

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await http.GetAsync(path, Ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    public void Dispose() => http.Dispose();
}

public static class ResponseExtensions
{
    /// <summary>Reads via string (not ReadFromJsonAsync, which disposes the cached stream) so it can be called repeatedly.</summary>
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    /// <summary>The stable error code from Problem Details (null if absent).</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var json = await response.JsonAsync();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
