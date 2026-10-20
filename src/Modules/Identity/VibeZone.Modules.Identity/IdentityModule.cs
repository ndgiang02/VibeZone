using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Modules;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Infrastructure.Security;
using VibeZone.Modules.Identity.Application;
using VibeZone.Modules.Identity.Endpoints;
using VibeZone.Modules.Identity.Infrastructure;

namespace VibeZone.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public const string Schema = "identity";

    /// <summary>The default authentication scheme: session cookie, or bearer access token (ADR-0007).</summary>
    public const string AuthenticationScheme = AuthenticationSelector.Scheme;

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789_-";
                o.SignIn.RequireConfirmedEmail = true;
                o.Password.RequiredLength = Validation.PasswordMinLength;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));

        var cookieSecurity = CookieSecurity.From(configuration);
        services.AddAuthentication(AuthenticationSelector.Scheme)
            .AddPolicyScheme(AuthenticationSelector.Scheme, null, o => o.ForwardDefaultSelector = AuthenticationSelector.Select)
            .AddCookie(SessionAuthentication.Scheme, o => SessionAuthentication.Configure(o, cookieSecurity))
            .AddScheme<AuthenticationSchemeOptions, AccessTokenHandler>(AccessTokenHandler.SchemeName, null);
        services.AddSingleton<DataProtectionAccessTokens>();
        services.AddSingleton<IAccessTokens>(sp => sp.GetRequiredService<DataProtectionAccessTokens>());
        services.AddAuthorization();

        services.AddScoped<IUserAccounts, IdentityUserAccounts>();
        services.AddScoped<EfUserSessions>();
        services.AddScoped<IUserSessions>(sp => sp.GetRequiredService<EfUserSessions>());
        services.AddScoped<IUserProfiles, EfUserProfiles>();
        services.AddScoped<Contracts.IUserRoles, UserRoles>();
        services.AddScoped<Contracts.IUserAvatars, EfUserAvatars>();
        services.AddScoped<Contracts.IUserDirectory, EfUserDirectory>();
        services.AddScoped<IAccountEmails, AccountEmails>();
        services.AddSingleton<IEmailSendThrottle, MemoryEmailSendThrottle>();
        services.AddScoped<AccountService>();
        services.AddScoped<SessionService>();
        services.AddScoped<TokenService>();
        services.AddScoped<ProfileService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder api)
    {
        AuthEndpoints.Map(api);
        MeEndpoints.Map(api);
    }
}
