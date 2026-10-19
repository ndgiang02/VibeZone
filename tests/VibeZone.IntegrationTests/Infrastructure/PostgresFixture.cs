using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using VibeZone.Infrastructure.Persistence;
using VibeZone.Migrations;

[assembly: AssemblyFixture(typeof(VibeZone.IntegrationTests.Infrastructure.PostgresFixture))]

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>One PostgreSQL container per test run, migrated with the real migrations.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as docker-compose.yml so tests exercise the production engine version.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.6-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext(ConnectionString);
        await db.Database.MigrateAsync();
    }

    /// <summary>Creates an empty database (no migrations applied) on the same server.</summary>
    public async Task<string> CreateEmptyDatabaseAsync(string name)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public static AppDbContext CreateDbContext(string connectionString)
    {
        var dataSource = PersistenceExtensions.CreateDataSource(connectionString);
        var options = PersistenceExtensions.Configure(new DbContextOptionsBuilder<AppDbContext>(), dataSource);
        return new AppDbContext((DbContextOptions<AppDbContext>)options.Options, AppModules.Create());
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
