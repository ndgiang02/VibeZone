using System.Net;

namespace VibeZone.IntegrationTests.Infrastructure;

public sealed record TestUser(string Email, string Handle, string Password);

/// <summary>Creates real accounts through the public API (register → email → verify), never by writing to the DB.</summary>
public static class TestUsers
{
    public const string Password = "correct horse battery staple";

    public static TestUser New(string prefix = "u")
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return new TestUser($"{prefix}-{id}@example.test", $"{prefix}{id}", Password);
    }

    public static async Task<TestUser> RegisterAsync(ApiClient client, string locale = "en", string prefix = "u")
    {
        var user = New(prefix);
        using var response = await client.PostAsync("/api/v1/auth/register", new { user.Email, user.Handle, user.Password, displayName = "Test User", locale });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return user;
    }

    public static async Task<TestUser> RegisterVerifiedAsync(ApiClient client, MailpitFixture mail, string locale = "en")
    {
        var user = await RegisterAsync(client, locale);
        var email = await mail.WaitForMessageAsync(user.Email, locale == "vi" ? "Xác nhận" : "Confirm");
        using var verify = await client.PostAsync("/api/v1/auth/verify-email", new { userId = Guid.Parse(email.Query("userId")), token = email.Query("token") });
        Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        return user;
    }

    public static async Task<TestUser> SignedInAsync(ApiClient client, MailpitFixture mail)
    {
        var user = await RegisterVerifiedAsync(client, mail);
        Assert.Equal(HttpStatusCode.NoContent, await client.LoginAsync(user.Email, user.Password));
        return user;
    }
}
