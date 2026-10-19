namespace VibeZone.SharedKernel;

public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests,
}

/// <summary>
/// An expected, client-facing failure. <see cref="Code"/> is a stable snake_case identifier the web app
/// translates; <see cref="Message"/> is an English fallback. Neither may contain internal details.
/// <see cref="Field"/> names the offending request field (camelCase) for validation errors.
/// </summary>
public sealed record DomainError(string Code, string Message, ErrorKind Kind, string? Field = null)
{
    public static DomainError Validation(string code, string message, string? field = null) => new(code, message, ErrorKind.Validation, field);
    public static DomainError Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);
    public static DomainError Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);
    public static DomainError NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);
    public static DomainError Conflict(string code, string message, string? field = null) => new(code, message, ErrorKind.Conflict, field);
}
