using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VibeZone.Infrastructure.Modules;

namespace VibeZone.Infrastructure.Persistence;

/// <summary>
/// Single context composed from every registered module's entity configurations (ADR-0001).
/// Modules access only their own entity sets.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ModuleCatalog modules)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string InfraSchema = "infra";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>ASP.NET Core Data Protection keys (cookies, antiforgery, Identity tokens) shared by all instances.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys", InfraSchema);
        modelBuilder.ApplyConfiguration(new Outbox.OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new Outbox.OutboxConsumptionConfiguration());

        foreach (var module in modules.Modules)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(module.Assembly);
        }
    }
}
