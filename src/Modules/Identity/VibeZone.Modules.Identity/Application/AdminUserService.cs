using VibeZone.Modules.Identity.Domain;
using VibeZone.Modules.Moderation.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

/// <summary>Staff view of an account (admins only: includes the email address).</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string Email,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    string? AvatarUrl);

/// <summary>Account administration (ASP.NET Core Identity in Infrastructure).</summary>
public interface IUserAdministration
{
    /// <summary>Newest first. <paramref name="query"/> matches handle, display name or email (contains).</summary>
    Task<Paged<AdminUserDto>> PageAsync(string? query, string? role, PageRequest page, CancellationToken cancellationToken);

    Task<AdminUserDto?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds or removes the role and saves (committing anything else staged in the unit of work, e.g. the audit
    /// entry). Takes effect on the user's sessions immediately.
    /// </summary>
    Task SetRoleAsync(Guid userId, string role, bool member, CancellationToken cancellationToken);
}

/// <param name="Role">Moderator or Admin.</param>
/// <param name="Locale">Language of the invitation email (and the new profile).</param>
public sealed record InviteStaffRequest(string? Email, string? Handle, string? DisplayName, string? Role, string? Locale);

public sealed class AdminUserService(IUserAdministration users, IUserAccounts accounts, IAccountEmails emails, IAuditLog audit)
{
    /// <summary>Roles staff can assign. Artist comes with an artist profile (self-serve) and isn't assigned by hand.</summary>
    public static readonly IReadOnlyList<string> AssignableRoles = [Roles.Moderator, Roles.Admin];

    private static readonly DomainError UserNotFound = DomainError.NotFound("user_not_found", "User not found.");

    public Task<Paged<AdminUserDto>> ListAsync(string? query, string? role, PageRequest page, CancellationToken cancellationToken) =>
        users.PageAsync(string.IsNullOrWhiteSpace(query) ? null : query.Trim(), NormalizeRole(role), page, cancellationToken);

    public async Task<Result<AdminUserDto>> SetRoleAsync(Guid actorUserId, Guid userId, string role, bool grant, CancellationToken cancellationToken)
    {
        if (NormalizeRole(role) is not { } name || !AssignableRoles.Contains(name))
        {
            return DomainError.Validation("role_not_assignable", "Only Moderator and Admin can be assigned.", "role");
        }

        if (!grant && name == Roles.Admin && actorUserId == userId)
        {
            return DomainError.Validation("role_self_admin", "You can't remove your own Admin role.", "role");
        }

        var user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return UserNotFound;
        }

        if (user.Roles.Contains(name) != grant)
        {
            audit.Stage(actorUserId, grant ? AuditActions.RoleGranted : AuditActions.RoleRevoked, "user", userId.ToString(),
                new Dictionary<string, string> { ["role"] = name });
            await users.SetRoleAsync(userId, name, grant, cancellationToken);
        }

        return await users.FindAsync(userId, cancellationToken) is { } updated ? updated : UserNotFound;
    }

    /// <summary>
    /// Creates a staff account with the role and emails the invitee a single-use link to choose a password. The
    /// account has no password (it can't sign in) and an unconfirmed email until the link is used; admins never
    /// see or set it. Audited as <c>user_invited</c>.
    /// </summary>
    public async Task<Result<AdminUserDto>> InviteAsync(Guid actorUserId, InviteStaffRequest request, CancellationToken cancellationToken)
    {
        var role = NormalizeRole(request.Role);
        var email = Validation.NormalizeEmail(request.Email);
        var handle = Handle.Normalize(request.Handle);
        var displayName = UserProfile.NormalizeDisplayName(string.IsNullOrWhiteSpace(request.DisplayName) ? handle : request.DisplayName);
        var locale = Locales.Normalize(request.Locale);
        var invalid = Validation.Email(email) ?? Handle.Validate(handle) ?? UserProfile.ValidateDisplayName(displayName)
            ?? (role is not null && AssignableRoles.Contains(role) ? null : DomainError.Validation("role_not_assignable", "Only Moderator and Admin can be assigned.", "role"));
        if (invalid is not null)
        {
            return invalid;
        }

        // Admins can already look accounts up by email, so saying it exists leaks nothing.
        if (await accounts.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return DomainError.Conflict("email_taken", "An account with this email already exists. Grant the role from the user list.", "email");
        }

        var created = await accounts.CreateAsync(new NewAccount(email, handle, null, displayName, locale), cancellationToken);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        var account = created.Value;
        audit.Stage(actorUserId, AuditActions.UserInvited, "user", account.Id.ToString(), new Dictionary<string, string> { ["role"] = role! });
        await users.SetRoleAsync(account.Id, role!, true, cancellationToken);

        var token = await accounts.CreateInviteTokenAsync(account.Id, cancellationToken);
        await emails.SendStaffInviteAsync(account, locale, role!, token, cancellationToken);
        return await users.FindAsync(account.Id, cancellationToken) is { } user ? user : UserNotFound;
    }

    /// <summary>
    /// Operator command (<c>grant-role</c>): grants a staff role without a signed-in admin, e.g. the very first Admin.
    /// Audited with the user as actor and <c>via=cli</c>. Only confirmed accounts.
    /// </summary>
    public async Task<Result<AdminUserDto>> GrantFromCommandLineAsync(Account? account, string role, CancellationToken cancellationToken)
    {
        if (NormalizeRole(role) is not { } name || !AssignableRoles.Contains(name))
        {
            return DomainError.Validation("role_not_assignable", "Only Moderator and Admin can be assigned.", "role");
        }

        if (account is not { EmailConfirmed: true } || await users.FindAsync(account.Id, cancellationToken) is not { } user)
        {
            return UserNotFound;
        }

        if (!user.Roles.Contains(name))
        {
            audit.Stage(user.Id, AuditActions.RoleGranted, "user", user.Id.ToString(), new Dictionary<string, string> { ["role"] = name, ["via"] = "cli" });
            await users.SetRoleAsync(user.Id, name, true, cancellationToken);
        }

        return await users.FindAsync(user.Id, cancellationToken) is { } updated ? updated : UserNotFound;
    }

    private static string? NormalizeRole(string? role) =>
        Roles.All.FirstOrDefault(r => string.Equals(r, role?.Trim(), StringComparison.OrdinalIgnoreCase));
}
