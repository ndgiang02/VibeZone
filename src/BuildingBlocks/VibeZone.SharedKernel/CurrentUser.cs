namespace VibeZone.SharedKernel;

/// <summary>The authenticated caller, if any. Implemented by the host from the validated session.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>The server-side session backing this request (null when anonymous).</summary>
    Guid? SessionId { get; }

    bool IsAuthenticated => UserId is not null;

    bool IsInRole(string role);

    /// <summary>For code paths already protected by authorization; throws if anonymous.</summary>
    Guid RequireUserId() => UserId ?? throw new InvalidOperationException("No authenticated user.");
}

public static class Roles
{
    public const string Artist = "Artist";
    public const string Moderator = "Moderator";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Artist, Moderator, Admin];
}
