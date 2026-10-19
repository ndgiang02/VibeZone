using System.Net.Mail;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

internal static class Validation
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;
    public const int EmailMaxLength = 254;

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim();

    public static DomainError? Email(string email, string field = "email") =>
        email.Length is 0 or > EmailMaxLength || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email
            ? DomainError.Validation("email_invalid", "Enter a valid email address.", field)
            : null;

    /// <summary>NIST 800-63B style: length only, no composition rules.</summary>
    public static DomainError? Password(string? password, string field = "password") => password switch
    {
        null or { Length: < PasswordMinLength } => DomainError.Validation("password_too_short", $"Password must be at least {PasswordMinLength} characters.", field),
        { Length: > PasswordMaxLength } => DomainError.Validation("password_too_long", $"Password must be at most {PasswordMaxLength} characters.", field),
        _ => null,
    };

    public static DomainError? Required(string? value, string field) =>
        string.IsNullOrEmpty(value) ? DomainError.Validation("required", "This field is required.", field) : null;
}
