using Microsoft.AspNetCore.Identity;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Infrastructure;

internal sealed class IdentityUserAccounts(
    UserManager<AppUser> users,
    IPasswordHasher<AppUser> hasher,
    AppDbContext db,
    TimeProvider time) : IUserAccounts
{
    private const string InvitePurpose = "StaffInvite";

    // A real hash of a random password, computed once: verifying against it costs the same as a real login.
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString()));

    public async Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        ToAccount(await users.FindByEmailAsync(email));

    public async Task<Account?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        ToAccount(await users.FindByIdAsync(userId.ToString()));

    public async Task<Result<Account>> CreateAsync(NewAccount account, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var user = new AppUser { UserName = account.Handle, Email = account.Email, CreatedAt = now };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = account.Password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, account.Password);
        if (!result.Succeeded)
        {
            return MapErrors(result);
        }

        db.Set<UserProfile>().Add(UserProfile.Create(user.Id, account.DisplayName, account.Locale, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAccount(user)!;
    }

    public void BurnPasswordHash(string password) => hasher.HashPassword(new AppUser(), password);

    public async Task<PasswordCheck> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new AppUser(), DummyHash.Value, password);
            return new PasswordCheck(PasswordCheckOutcome.Invalid, null);
        }

        if (await users.IsLockedOutAsync(user))
        {
            hasher.VerifyHashedPassword(user, DummyHash.Value, password);
            return new PasswordCheck(PasswordCheckOutcome.LockedOut, null);
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return new PasswordCheck(PasswordCheckOutcome.Invalid, null);
        }

        await users.ResetAccessFailedCountAsync(user);
        return user.EmailConfirmed
            ? new PasswordCheck(PasswordCheckOutcome.Succeeded, ToAccount(user))
            : new PasswordCheck(PasswordCheckOutcome.EmailNotConfirmed, ToAccount(user));
    }

    public async Task<string> CreateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GenerateEmailConfirmationTokenAsync(await RequireAsync(userId));

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && (user.EmailConfirmed || (await users.ConfirmEmailAsync(user, token)).Succeeded);
    }

    public async Task<string> CreatePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GeneratePasswordResetTokenAsync(await RequireAsync(userId));

    public async Task<Result> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? AccountService.InvalidToken
                : MapErrors(result, "newPassword"));
        }

        // A reset proves mailbox control; clear any lock-out the attacker may have caused.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var result = await users.ChangePasswordAsync(await RequireAsync(userId), currentPassword, newPassword);
        if (result.Succeeded)
        {
            return Result.Success();
        }

        return Result.Failure(result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
            ? DomainError.Validation("current_password_incorrect", "Current password is incorrect.", "currentPassword")
            : MapErrors(result, "newPassword"));
    }

    public async Task<string> CreateInviteTokenAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.GenerateUserTokenAsync(await RequireAsync(userId), TokenOptions.DefaultProvider, InvitePurpose);

    public async Task<Result> AcceptInviteAsync(Guid userId, string token, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || await users.HasPasswordAsync(user) || !await users.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, InvitePurpose, token))
        {
            return Result.Failure(AccountService.InvalidToken);
        }

        // Saved together with the password; the new security stamp spends the token.
        user.EmailConfirmed = true;
        var result = await users.AddPasswordAsync(user, password);
        return result.Succeeded ? Result.Success() : Result.Failure(MapErrors(result));
    }

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken) =>
        RoleQueries.ForUserAsync(db, userId, cancellationToken);

    private async Task<AppUser> RequireAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");

    private static Account? ToAccount(AppUser? user) =>
        user is null ? null : new Account(user.Id, user.Email!, user.UserName!, user.EmailConfirmed);

    private static DomainError MapErrors(IdentityResult result, string passwordField = "password")
    {
        var codes = result.Errors.Select(e => e.Code).ToHashSet();
        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return DomainError.Conflict("handle_taken", "This handle is already taken.", "handle");
        }

        if (codes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            return DomainError.Validation("handle_invalid", "Invalid handle.", "handle");
        }

        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            // Lost a race with a concurrent registration of the same address; don't confirm it exists.
            return DomainError.Conflict("registration_failed", "Registration could not be completed. Try again.");
        }

        if (codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal)))
        {
            return DomainError.Validation("password_invalid", "Password does not meet the requirements.", passwordField);
        }

        return DomainError.Validation("registration_failed", "Registration could not be completed.");
    }
}
