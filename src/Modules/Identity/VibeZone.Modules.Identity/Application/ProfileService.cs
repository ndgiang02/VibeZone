using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Application;

public sealed record MeDto(
    Guid Id,
    string Email,
    string Handle,
    string DisplayName,
    string Locale,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles);

public sealed record UpdateProfileRequest(string? DisplayName, string? Locale);

public sealed class ProfileService(IUserAccounts accounts, IUserProfiles profiles, TimeProvider time)
{
    public async Task<Result<MeDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(userId, cancellationToken);
        var profile = await profiles.FindAsync(userId, cancellationToken);
        if (account is null || profile is null)
        {
            return DomainError.NotFound("user_not_found", "User not found.");
        }

        var roles = await accounts.GetRolesAsync(userId, cancellationToken);
        return new MeDto(account.Id, account.Email, account.Handle, profile.DisplayName, profile.Locale, account.EmailConfirmed, roles);
    }

    public async Task<Result<MeDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var displayName = UserProfile.NormalizeDisplayName(request.DisplayName);
        if (UserProfile.ValidateDisplayName(displayName) is { } invalid)
        {
            return invalid;
        }

        if (request.Locale is not null && !Locales.Supported.Contains(request.Locale))
        {
            return DomainError.Validation("locale_unsupported", "Unsupported language.", "locale");
        }

        var profile = await profiles.FindAsync(userId, cancellationToken);
        if (profile is null)
        {
            return DomainError.NotFound("user_not_found", "User not found.");
        }

        profile.Update(displayName, request.Locale ?? profile.Locale, time.GetUtcNow());
        await profiles.SaveChangesAsync(cancellationToken);
        return await GetMeAsync(userId, cancellationToken);
    }
}
