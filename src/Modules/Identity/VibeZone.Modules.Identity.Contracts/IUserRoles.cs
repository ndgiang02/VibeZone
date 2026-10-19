namespace VibeZone.Modules.Identity.Contracts;

/// <summary>Role assignment for other modules (e.g. Catalog grants <c>Artist</c> on profile creation).</summary>
public interface IUserRoles
{
    /// <summary>Idempotent. Takes effect on the user's existing sessions immediately on this instance.</summary>
    Task GrantAsync(Guid userId, string role, CancellationToken cancellationToken);
}
