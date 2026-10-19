using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using VibeZone.Infrastructure.Persistence;

namespace VibeZone.Migrations;

/// <summary>
/// Used by <c>dotnet ef</c>. Connection string comes from <c>ConnectionStrings__Database</c> or the
/// API's user-secrets; <c>migrations add</c> works without a database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string OfflinePlaceholder = "Host=localhost;Database=vibezone_design_time";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        var dataSource = PersistenceExtensions.CreateDataSource(string.IsNullOrWhiteSpace(connectionString) ? OfflinePlaceholder : connectionString);

        var options = PersistenceExtensions.Configure(new DbContextOptionsBuilder<AppDbContext>(), dataSource);
        return new AppDbContext((DbContextOptions<AppDbContext>)options.Options, AppModules.Create());
    }
}
