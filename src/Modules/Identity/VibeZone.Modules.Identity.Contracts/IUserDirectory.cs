namespace VibeZone.Modules.Identity.Contracts;

/// <param name="AvatarUrl">Small account picture, or null.</param>
public sealed record UserSummary(Guid Id, string DisplayName, string Handle, string? AvatarUrl);

/// <summary>Public account details for other modules (e.g. playlist owners).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>The account's public identity, or null if the user doesn't exist.</summary>
    Task<PublicProfile?> FindProfileAsync(Guid userId, CancellationToken cancellationToken);
}
