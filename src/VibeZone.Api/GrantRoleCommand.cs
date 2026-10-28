using VibeZone.Modules.Identity.Application;

namespace VibeZone.Api;

/// <summary>
/// Operator command: <c>dotnet run --project src/VibeZone.Api -- grant-role &lt;email&gt; &lt;Moderator|Admin&gt;</c>
/// (in a container: <c>dotnet VibeZone.Api.dll grant-role …</c>). Bootstraps the first Admin, who then manages
/// roles in the admin dashboard. Runs against the configured database in any environment, so it needs the same
/// access as the app; the grant is audited (<c>via=cli</c>). Idempotent.
/// </summary>
internal static class GrantRoleCommand
{
    public const string Command = "grant-role";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var position = Array.IndexOf(args, Command);
        if (args.Length < position + 3)
        {
            app.Logger.LogError("Usage: {Command} <email> <Moderator|Admin>", Command);
            return 2;
        }

        var (email, role) = (args[position + 1], args[position + 2]);
        var ct = CancellationToken.None;
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var account = await services.GetRequiredService<IUserAccounts>().FindByEmailAsync(email, ct);
        var result = await services.GetRequiredService<AdminUserService>().GrantFromCommandLineAsync(account, role, ct);
        if (!result.IsSuccess)
        {
            // The email is the operator's own input, but logs may be shipped elsewhere: don't repeat it.
            app.Logger.LogError("Role not granted: {Code} (the account must exist and have a confirmed email).", result.Error!.Code);
            return 1;
        }

        app.Logger.LogInformation("Granted {Role} to user {UserId}. Roles now: {Roles}", role, result.Value.Id, string.Join(", ", result.Value.Roles));
        return 0;
    }
}
