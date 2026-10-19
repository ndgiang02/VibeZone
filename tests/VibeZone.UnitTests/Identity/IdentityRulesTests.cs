using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.UnitTests.Identity;

public sealed class HandleTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("dj_mix-2026")]
    [InlineData("a1b")]
    public void Accepts_valid_handles(string handle) => Assert.Null(Handle.Validate(handle));

    [Theory]
    [InlineData("ab", "handle_invalid")]
    [InlineData("_abc", "handle_invalid")]
    [InlineData("abc-", "handle_invalid")]
    [InlineData("has space", "handle_invalid")]
    [InlineData("UPPER", "handle_invalid")]
    [InlineData("tiếng", "handle_invalid")]
    [InlineData("admin", "handle_reserved")]
    [InlineData("settings", "handle_reserved")]
    public void Rejects_invalid_or_reserved_handles(string handle, string code)
    {
        var error = Handle.Validate(handle);

        Assert.NotNull(error);
        Assert.Equal(code, error.Code);
        Assert.Equal("handle", error.Field);
    }

    [Fact]
    public void Rejects_handles_longer_than_30() => Assert.NotNull(Handle.Validate(new string('a', 31)));

    [Fact]
    public void Normalizes_case_and_whitespace() => Assert.Equal("djvibe", Handle.Normalize("  DJVibe "));
}

public sealed class UserSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_session_is_active_until_its_absolute_expiry()
    {
        var session = UserSession.Start(Guid.NewGuid(), "Agent", Now);

        Assert.True(session.IsActive(Now));
        Assert.True(session.IsActive(Now + UserSession.AbsoluteLifetime - TimeSpan.FromSeconds(1)));
        Assert.False(session.IsActive(Now + UserSession.AbsoluteLifetime));
    }

    [Fact]
    public void Revoked_session_is_inactive_and_keeps_first_revocation_time()
    {
        var session = UserSession.Start(Guid.NewGuid(), null, Now);

        session.Revoke(Now.AddMinutes(1));
        session.Revoke(Now.AddMinutes(5));

        Assert.False(session.IsActive(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
    }

    [Fact]
    public void Long_user_agents_are_truncated()
    {
        var session = UserSession.Start(Guid.NewGuid(), new string('x', 1000), Now);

        Assert.Equal(UserSession.MaxUserAgentLength, session.UserAgent!.Length);
    }

    [Fact]
    public void Last_seen_is_only_touched_after_the_interval()
    {
        var session = UserSession.Start(Guid.NewGuid(), null, Now);

        Assert.False(session.NeedsTouch(Now + UserSession.TouchInterval - TimeSpan.FromSeconds(1)));
        Assert.True(session.NeedsTouch(Now + UserSession.TouchInterval));
    }
}

public sealed class ValidationTests
{
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("first.last+tag@sub.example.vn", true)]
    [InlineData("no-at-sign", false)]
    [InlineData("Name <user@example.com>", false)]
    [InlineData("", false)]
    public void Validates_email_addresses(string email, bool valid) => Assert.Equal(valid, Validation.Email(email) is null);

    [Theory]
    [InlineData(null, "password_too_short")]
    [InlineData("123456789", "password_too_short")]
    [InlineData("1234567890", null)]
    [InlineData("mật khẩu dài đủ", null)]
    public void Validates_password_length_only(string? password, string? code) =>
        Assert.Equal(code, Validation.Password(password)?.Code);

    [Fact]
    public void Rejects_passwords_over_128_characters() =>
        Assert.Equal("password_too_long", Validation.Password(new string('a', 129))?.Code);

    [Theory]
    [InlineData("en", "en")]
    [InlineData(" EN ", "en")]
    [InlineData("vi", "vi")]
    [InlineData("fr", "vi")]
    [InlineData(null, "vi")]
    public void Normalizes_locales_to_supported_with_vietnamese_default(string? input, string expected) =>
        Assert.Equal(expected, Locales.Normalize(input));

    [Fact]
    public void Display_name_collapses_whitespace_and_enforces_length()
    {
        Assert.Equal("Nguyễn Văn A", UserProfile.NormalizeDisplayName("  Nguyễn \t Văn   A "));
        Assert.NotNull(UserProfile.ValidateDisplayName(""));
        Assert.NotNull(UserProfile.ValidateDisplayName(new string('a', UserProfile.MaxDisplayNameLength + 1)));
        Assert.Null(UserProfile.ValidateDisplayName("Ok"));
    }
}
