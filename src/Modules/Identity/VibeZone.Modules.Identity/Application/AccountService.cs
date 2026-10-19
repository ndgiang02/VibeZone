using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

public sealed record RegisterRequest(string? Email, string? Handle, string? Password, string? DisplayName, string? Locale);

public sealed record ConfirmEmailRequest(Guid UserId, string? Token);

public sealed record EmailOnlyRequest(string? Email, string? Locale);

public sealed record ResetPasswordRequest(string? Email, string? Token, string? NewPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// Account lifecycle. Endpoints that take an email address (register, resend, forgot) succeed
/// identically whether or not an account exists — the only signal goes to the mailbox owner.
/// </summary>
public sealed class AccountService(
    IUserAccounts accounts,
    IUserSessions sessions,
    IUserProfiles profiles,
    IAccountEmails emails,
    IEmailSendThrottle throttle,
    TimeProvider time)
{
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = Validation.NormalizeEmail(request.Email);
        var handle = Handle.Normalize(request.Handle);
        var displayName = UserProfile.NormalizeDisplayName(string.IsNullOrWhiteSpace(request.DisplayName) ? handle : request.DisplayName);
        var locale = Locales.Normalize(request.Locale);

        var invalid = Validation.Email(email) ?? Handle.Validate(handle) ?? Validation.Password(request.Password) ?? UserProfile.ValidateDisplayName(displayName);
        if (invalid is not null)
        {
            return Result.Failure(invalid);
        }

        var existing = await accounts.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            accounts.BurnPasswordHash(request.Password!);
            if (throttle.TryAcquire("account-exists", email))
            {
                var existingLocale = (await profiles.FindAsync(existing.Id, cancellationToken))?.Locale ?? locale;
                await emails.SendAccountExistsAsync(existing.Email, existingLocale, cancellationToken);
            }

            return Result.Success();
        }

        var created = await accounts.CreateAsync(new NewAccount(email, handle, request.Password!, displayName, locale), cancellationToken);
        if (!created.IsSuccess)
        {
            return Result.Failure(created.Error);
        }

        await SendVerificationAsync(created.Value, locale, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token) || !await accounts.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken))
        {
            return Result.Failure(InvalidToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResendVerificationAsync(EmailOnlyRequest request, CancellationToken cancellationToken)
    {
        var email = Validation.NormalizeEmail(request.Email);
        if (Validation.Email(email) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var account = await accounts.FindByEmailAsync(email, cancellationToken);
        if (account is { EmailConfirmed: false })
        {
            await SendVerificationAsync(account, await LocaleOfAsync(account.Id, request.Locale, cancellationToken), cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(EmailOnlyRequest request, CancellationToken cancellationToken)
    {
        var email = Validation.NormalizeEmail(request.Email);
        if (Validation.Email(email) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var account = await accounts.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return Result.Success();
        }

        var locale = await LocaleOfAsync(account.Id, request.Locale, cancellationToken);
        if (!account.EmailConfirmed)
        {
            // Resetting would also prove ownership; verifying first keeps a single trust path.
            await SendVerificationAsync(account, locale, cancellationToken);
        }
        else if (throttle.TryAcquire("password-reset", account.Email))
        {
            var token = await accounts.CreatePasswordResetTokenAsync(account.Id, cancellationToken);
            await emails.SendPasswordResetAsync(account, locale, token, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (Validation.Password(request.NewPassword, "newPassword") is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var account = string.IsNullOrEmpty(request.Token) ? null : await accounts.FindByEmailAsync(Validation.NormalizeEmail(request.Email), cancellationToken);
        if (account is null)
        {
            return Result.Failure(InvalidToken);
        }

        var reset = await accounts.ResetPasswordAsync(account.Id, request.Token!, request.NewPassword!, cancellationToken);
        if (!reset.IsSuccess)
        {
            return reset;
        }

        // Anyone holding an old session (e.g. the reason for the reset) is signed out everywhere.
        await sessions.RevokeAllAsync(account.Id, exceptSessionId: null, time.GetUtcNow(), cancellationToken);
        await emails.SendPasswordChangedAsync(account, await LocaleOfAsync(account.Id, null, cancellationToken), cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, Guid currentSessionId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validation.Required(request.CurrentPassword, "currentPassword") ?? Validation.Password(request.NewPassword, "newPassword");
        if (invalid is not null)
        {
            return Result.Failure(invalid);
        }

        var changed = await accounts.ChangePasswordAsync(userId, request.CurrentPassword!, request.NewPassword!, cancellationToken);
        if (!changed.IsSuccess)
        {
            return changed;
        }

        await sessions.RevokeAllAsync(userId, exceptSessionId: currentSessionId, time.GetUtcNow(), cancellationToken);
        if (await accounts.FindByIdAsync(userId, cancellationToken) is { } account)
        {
            await emails.SendPasswordChangedAsync(account, await LocaleOfAsync(userId, null, cancellationToken), cancellationToken);
        }

        return Result.Success();
    }

    internal static readonly DomainError InvalidToken =
        DomainError.Validation("token_invalid", "This link is invalid or has expired. Request a new one.");

    private async Task SendVerificationAsync(Account account, string locale, CancellationToken cancellationToken)
    {
        if (throttle.TryAcquire("verify-email", account.Email))
        {
            var token = await accounts.CreateEmailConfirmationTokenAsync(account.Id, cancellationToken);
            await emails.SendEmailVerificationAsync(account, locale, token, cancellationToken);
        }
    }

    private async Task<string> LocaleOfAsync(Guid userId, string? fallback, CancellationToken cancellationToken) =>
        (await profiles.FindAsync(userId, cancellationToken))?.Locale ?? Locales.Normalize(fallback);
}
