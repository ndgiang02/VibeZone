using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

[assembly: AssemblyFixture(typeof(VibeZone.IntegrationTests.Infrastructure.MailpitFixture))]

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>Real SMTP server + inbox API (same image as docker-compose.yml). Tests read the emails users would get.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
        .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(HttpPort).ForPath("/readyz")))
        .Build();

    private HttpClient _http = null!;

    public string SmtpHost => _container.Hostname;

    public int SmtpMappedPort => _container.GetMappedPublicPort(SmtpPort);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}") };
    }

    /// <summary>Waits for an email to <paramref name="to"/> whose subject contains <paramref name="subjectContains"/>.</summary>
    public async Task<MailMessage> WaitForMessageAsync(string to, string subjectContains, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            var match = (await SearchAsync(to)).FirstOrDefault(m => m.Subject.Contains(subjectContains, StringComparison.Ordinal));
            if (match is not null)
            {
                return (await _http.GetFromJsonAsync<MailMessage>($"/api/v1/message/{match.Id}"))!;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No email to {to} with subject containing '{subjectContains}'.");
    }

    public async Task<IReadOnlyList<MailSummary>> SearchAsync(string to)
    {
        var result = await _http.GetFromJsonAsync<MailSearchResult>($"/api/v1/search?query={Uri.EscapeDataString($"to:\"{to}\"")}");
        return result?.Messages ?? [];
    }

    public ValueTask DisposeAsync()
    {
        _http?.Dispose();
        return _container.DisposeAsync();
    }

    public sealed record MailSearchResult([property: JsonPropertyName("messages")] IReadOnlyList<MailSummary> Messages);

    public sealed record MailSummary([property: JsonPropertyName("ID")] string Id, [property: JsonPropertyName("Subject")] string Subject);

    public sealed record MailMessage(
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("Text")] string Text,
        [property: JsonPropertyName("HTML")] string Html)
    {
        /// <summary>The first link in the plain-text body (all VibeZone emails have exactly one call to action).</summary>
        public Uri Link => new(System.Text.RegularExpressions.Regex.Match(Text, @"https?://\S+").Value);

        public string Query(string name) =>
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(Link.Query).TryGetValue(name, out var v) ? v.ToString() : throw new KeyNotFoundException(name);
    }
}
