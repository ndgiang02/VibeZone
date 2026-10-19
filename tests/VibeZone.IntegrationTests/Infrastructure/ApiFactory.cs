using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>Boots the real API (environment "Testing": no user-secrets) against a given database.</summary>
public sealed class ApiFactory(string? connectionString, IReadOnlyDictionary<string, string>? settings = null, MailpitFixture? mailpit = null)
    : WebApplicationFactory<Program>
{
    public const string PublicBaseUrl = "https://web.vibezone.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Database", connectionString);
        }

        // Valid defaults; SMTP points at the shared Mailpit container when the test uses email.
        builder.UseSetting("App:PublicBaseUrl", PublicBaseUrl);
        builder.UseSetting("Email:From", "VibeZone <no-reply@vibezone.test>");
        builder.UseSetting("Email:SmtpHost", mailpit?.SmtpHost ?? "smtp.invalid");
        builder.UseSetting("Email:SmtpPort", (mailpit?.SmtpMappedPort ?? 1025).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Email:SmtpSecurity", "None");
        // Storage is required configuration since M3; these tests don't touch media (no object store is reachable).
        builder.UseSetting("Storage:ServiceUrl", "http://storage.invalid:9000");
        builder.UseSetting("Storage:Bucket", "vibezone-test");
        builder.UseSetting("Storage:AccessKey", "test-access-key");
        builder.UseSetting("Storage:SecretKey", "test-secret-key");
        // Most tests make many auth calls from one (test-server) IP; rate-limit tests lower this explicitly.
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "1000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>A browser-like client: HTTPS (so __Host- cookies are kept), cookie jar, CSRF header handling.</summary>
    public ApiClient CreateApiClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false }));
}
