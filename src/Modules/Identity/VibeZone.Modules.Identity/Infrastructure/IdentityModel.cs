using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Modules.Identity.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Identity.Infrastructure;

/// <summary>Persistence model for ASP.NET Core Identity. <c>UserName</c> holds the public handle.</summary>
internal sealed class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = EntityId.New();

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class AppRole : IdentityRole<Guid>;

/// <summary>Roles are reference data with fixed ids so migrations are deterministic.</summary>
internal static class RoleReferenceData
{
    public static readonly AppRole[] All =
    [
        Role("0195f3a0-0000-7000-8000-000000000001", Roles.Artist),
        Role("0195f3a0-0000-7000-8000-000000000002", Roles.Moderator),
        Role("0195f3a0-0000-7000-8000-000000000003", Roles.Admin),
    ];

    private static AppRole Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}

// The configurations below mirror IdentityDbContext's model (schema v3) because AppDbContext is composed
// from modules instead of inheriting IdentityDbContext. Tables live in the "identity" schema.

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("users", IdentityModule.Schema);
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.HasIndex(u => u.NormalizedUserName).IsUnique();
        b.HasIndex(u => u.NormalizedEmail).IsUnique();
        b.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
        b.Property(u => u.UserName).HasMaxLength(256);
        b.Property(u => u.NormalizedUserName).HasMaxLength(256);
        b.Property(u => u.Email).HasMaxLength(256);
        b.Property(u => u.NormalizedEmail).HasMaxLength(256);
        b.Property(u => u.PhoneNumber).HasMaxLength(256);

        b.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
        b.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
        b.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
        b.HasMany<IdentityUserRole<Guid>>().WithOne().HasForeignKey(r => r.UserId).IsRequired();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.ToTable("roles", IdentityModule.Schema);
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.HasIndex(r => r.NormalizedName).IsUnique();
        b.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();
        b.Property(r => r.Name).HasMaxLength(256);
        b.Property(r => r.NormalizedName).HasMaxLength(256);

        b.HasMany<IdentityUserRole<Guid>>().WithOne().HasForeignKey(ur => ur.RoleId).IsRequired();
        b.HasMany<IdentityRoleClaim<Guid>>().WithOne().HasForeignKey(rc => rc.RoleId).IsRequired();
        b.HasData(RoleReferenceData.All);
    }
}

internal sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b)
    {
        b.ToTable("user_claims", IdentityModule.Schema);
        b.HasKey(c => c.Id);
    }
}

internal sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b)
    {
        b.ToTable("user_logins", IdentityModule.Schema);
        b.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        b.Property(l => l.LoginProvider).HasMaxLength(128);
        b.Property(l => l.ProviderKey).HasMaxLength(128);
    }
}

internal sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b)
    {
        b.ToTable("user_tokens", IdentityModule.Schema);
        b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        b.Property(t => t.LoginProvider).HasMaxLength(128);
        b.Property(t => t.Name).HasMaxLength(128);
    }
}

internal sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> b)
    {
        b.ToTable("user_roles", IdentityModule.Schema);
        b.HasKey(r => new { r.UserId, r.RoleId });
    }
}

internal sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> b)
    {
        b.ToTable("role_claims", IdentityModule.Schema);
        b.HasKey(c => c.Id);
    }
}

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("user_profiles", IdentityModule.Schema, t => t.HasCheckConstraint("ck_user_profiles_locale", "locale IN ('vi', 'en')"));
        b.HasKey(p => p.UserId);
        b.Property(p => p.DisplayName).HasMaxLength(UserProfile.MaxDisplayNameLength).IsRequired();
        b.Property(p => p.Locale).HasMaxLength(8).IsRequired();
        b.Property<uint>("xmin").IsRowVersion();
        b.HasOne<AppUser>().WithOne().HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("user_sessions", IdentityModule.Schema);
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.UserAgent).HasMaxLength(UserSession.MaxUserAgentLength);
        b.HasIndex(s => s.UserId).HasFilter("revoked_at IS NULL");
        b.HasIndex(s => s.RefreshTokenHash).IsUnique().HasFilter("refresh_token_hash IS NOT NULL")
            .HasDatabaseName("ix_user_sessions_refresh_token_hash");
        b.HasIndex(s => s.PreviousRefreshTokenHash).HasFilter("previous_refresh_token_hash IS NOT NULL")
            .HasDatabaseName("ix_user_sessions_previous_refresh_token_hash");
        b.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
