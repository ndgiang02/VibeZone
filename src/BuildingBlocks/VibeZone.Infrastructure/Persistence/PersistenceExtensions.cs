using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace VibeZone.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string MigrationsAssembly = "VibeZone.Migrations";

    public static IServiceCollection AddVibeZonePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(o => o.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return CreateDataSource(options.ConnectionString);
        });

        services.AddDbContext<AppDbContext>((sp, builder) => Configure(builder, sp.GetRequiredService<NpgsqlDataSource>()));
        return services;
    }

    /// <summary>
    /// Npgsql tries GSSAPI (Kerberos) encryption by default, which logs a missing-libgssapi error in our
    /// slim images. We don't use Kerberos; it stays opt-in via "GSS Encryption Mode=..." in the connection string.
    /// </summary>
    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        if (!connectionString.Contains("GSS Encryption Mode", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.Build();
    }

    /// <summary>Shared by runtime and design-time (migrations) configuration so they cannot drift.</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, NpgsqlDataSource dataSource) =>
        builder
            .UseNpgsql(dataSource, npgsql => npgsql
                .MigrationsAssembly(MigrationsAssembly)
                .MigrationsHistoryTable(AppDbContext.MigrationsHistoryTable, AppDbContext.InfraSchema))
            .UseSnakeCaseNamingConvention();
}
