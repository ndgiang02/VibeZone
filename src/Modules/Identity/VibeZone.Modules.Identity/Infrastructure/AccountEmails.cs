using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VibeZone.Infrastructure.Email;
using VibeZone.Infrastructure.Web;
using VibeZone.Modules.Identity.Application;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Infrastructure;

internal sealed class AccountEmails(IEmailSender queue, IOptions<PublicUrlOptions> urls) : IAccountEmails
{
    public Task SendEmailVerificationAsync(Account account, string locale, string token, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link(locale, "verify-email", new Dictionary<string, string> { ["userId"] = account.Id.ToString(), ["token"] = token });
        var t = Text(locale);
        return SendAsync(account.Email, "verify-email", t.VerifySubject, t.VerifyIntro, t.VerifyAction, link, t.VerifyFooter, cancellationToken);
    }

    public Task SendAccountExistsAsync(string email, string locale, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link(locale, "forgot-password", new Dictionary<string, string>());
        var t = Text(locale);
        return SendAsync(email, "account-exists", t.ExistsSubject, t.ExistsIntro, t.ExistsAction, link, t.IgnoreFooter, cancellationToken);
    }

    public Task SendPasswordResetAsync(Account account, string locale, string token, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link(locale, "reset-password", new Dictionary<string, string> { ["email"] = account.Email, ["token"] = token });
        var t = Text(locale);
        return SendAsync(account.Email, "password-reset", t.ResetSubject, t.ResetIntro, t.ResetAction, link, t.ResetFooter, cancellationToken);
    }

    public Task SendPasswordChangedAsync(Account account, string locale, CancellationToken cancellationToken)
    {
        var link = urls.Value.Link(locale, "forgot-password", new Dictionary<string, string>());
        var t = Text(locale);
        return SendAsync(account.Email, "password-changed", t.ChangedSubject, t.ChangedIntro, t.ChangedAction, link, t.ChangedFooter, cancellationToken);
    }

    private async Task SendAsync(string to, string template, string subject, string intro, string action, string link, string footer, CancellationToken cancellationToken)
    {
        var text = $"{intro}\n\n{action}: {link}\n\n{footer}\n\n— VibeZone";
        Func<string, string> e = WebUtility.HtmlEncode;
        var html = $"""
            <!doctype html>
            <html><body style="margin:0;background:#0f0f11;font-family:Arial,Helvetica,sans-serif;color:#f4f4f5">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:32px 16px">
                <tr><td align="center">
                  <table role="presentation" width="100%" style="max-width:480px;background:#17171b;border-radius:12px;padding:32px">
                    <tr><td style="font-size:20px;font-weight:bold;padding-bottom:16px">Vibe<span style="color:#ff6b3d">Zone</span></td></tr>
                    <tr><td style="font-size:15px;line-height:1.6;color:#d4d4d8;padding-bottom:24px">{e(intro)}</td></tr>
                    <tr><td style="padding-bottom:24px">
                      <a href="{e(link)}" style="display:inline-block;background:#ff6b3d;color:#1a0d08;text-decoration:none;font-weight:bold;padding:12px 20px;border-radius:8px">{e(action)}</a>
                    </td></tr>
                    <tr><td style="font-size:12px;line-height:1.6;color:#a1a1aa;word-break:break-all">{e(link)}</td></tr>
                    <tr><td style="font-size:12px;line-height:1.6;color:#a1a1aa;padding-top:16px">{e(footer)}</td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;

        await queue.EnqueueAsync(new EmailMessage(to, subject, text, html, template), cancellationToken);
    }

    private static Texts Text(string locale) => locale == Locales.English ? Texts.En : Texts.Vi;

    private sealed record Texts(
        string VerifySubject, string VerifyIntro, string VerifyAction, string VerifyFooter,
        string ExistsSubject, string ExistsIntro, string ExistsAction,
        string ResetSubject, string ResetIntro, string ResetAction, string ResetFooter,
        string ChangedSubject, string ChangedIntro, string ChangedAction, string ChangedFooter,
        string IgnoreFooter)
    {
        public static readonly Texts En = new(
            "Confirm your VibeZone email",
            "Welcome to VibeZone! Confirm your email address to finish creating your account.",
            "Confirm email",
            "This link expires in 24 hours. If you didn't create an account, you can ignore this email.",
            "You already have a VibeZone account",
            "Someone (hopefully you) tried to create a VibeZone account with this email address, but an account already exists. If you forgot your password, you can reset it.",
            "Reset password",
            "Reset your VibeZone password",
            "We received a request to reset your password. Choose a new one using the link below.",
            "Choose a new password",
            "This link expires in 1 hour and can be used once. If you didn't request it, ignore this email — your password stays the same.",
            "Your VibeZone password was changed",
            "Your password was just changed and other devices were signed out. If this was you, no action is needed.",
            "Reset password",
            "If you didn't change it, reset your password immediately to secure your account.",
            "If this wasn't you, you can safely ignore this email.");

        public static readonly Texts Vi = new(
            "Xác nhận email VibeZone của bạn",
            "Chào mừng bạn đến với VibeZone! Hãy xác nhận địa chỉ email để hoàn tất việc tạo tài khoản.",
            "Xác nhận email",
            "Liên kết có hiệu lực trong 24 giờ. Nếu bạn không tạo tài khoản, hãy bỏ qua email này.",
            "Bạn đã có tài khoản VibeZone",
            "Ai đó (hy vọng là bạn) vừa thử tạo tài khoản VibeZone bằng địa chỉ email này, nhưng tài khoản đã tồn tại. Nếu bạn quên mật khẩu, bạn có thể đặt lại.",
            "Đặt lại mật khẩu",
            "Đặt lại mật khẩu VibeZone",
            "Chúng tôi nhận được yêu cầu đặt lại mật khẩu của bạn. Hãy chọn mật khẩu mới qua liên kết bên dưới.",
            "Chọn mật khẩu mới",
            "Liên kết có hiệu lực trong 1 giờ và chỉ dùng được một lần. Nếu bạn không yêu cầu, hãy bỏ qua email này — mật khẩu của bạn không thay đổi.",
            "Mật khẩu VibeZone của bạn đã được thay đổi",
            "Mật khẩu của bạn vừa được thay đổi và các thiết bị khác đã bị đăng xuất. Nếu đó là bạn, bạn không cần làm gì thêm.",
            "Đặt lại mật khẩu",
            "Nếu bạn không thực hiện thay đổi này, hãy đặt lại mật khẩu ngay để bảo vệ tài khoản.",
            "Nếu đó không phải bạn, bạn có thể bỏ qua email này.");
    }
}

/// <summary>At most <see cref="MaxPerWindow"/> emails of one purpose per address per hour (per instance).</summary>
internal sealed class MemoryEmailSendThrottle(IMemoryCache cache, TimeProvider time) : IEmailSendThrottle
{
    public const int MaxPerWindow = 3;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private readonly Lock _gate = new();

    public bool TryAcquire(string purpose, string email)
    {
        var key = $"identity:email-throttle:{purpose}:{email.ToUpperInvariant()}";
        lock (_gate)
        {
            var now = time.GetUtcNow();
            var sent = cache.Get<List<DateTimeOffset>>(key) ?? [];
            sent.RemoveAll(t => now - t >= Window);
            if (sent.Count >= MaxPerWindow)
            {
                return false;
            }

            sent.Add(now);
            cache.Set(key, sent, Window);
            return true;
        }
    }
}
