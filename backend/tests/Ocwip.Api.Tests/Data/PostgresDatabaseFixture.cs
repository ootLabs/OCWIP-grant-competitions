using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Data;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// A throwaway PostgreSQL database, created and migrated once per test class and
/// dropped afterwards.
///
/// Configuration tests that only read EF metadata prove that the configuration
/// agrees with itself. The invariants this project cares about (a unique index,
/// a foreign key that refuses to cascade, a jsonb round trip, timestamps that
/// survive a non UTC offset) live in PostgreSQL, so they need a real database.
///
/// The fixture stays silent when <c>ConnectionStrings__Postgres</c> is missing,
/// because xUnit builds class fixtures even when every test in the class is
/// skipped by [RequiresDatabaseFact]. Tests read <see cref="IsAvailable"/> only
/// through that attribute, never to pass without asserting.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private string? _maintenanceConnectionString;
    private string? _databaseName;

    public string? ConnectionString { get; private set; }

    public bool IsAvailable => ConnectionString is not null;

    public async Task InitializeAsync()
    {
        var baseConnectionString = RequiresDatabaseFactAttribute.ConnectionString;
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        _maintenanceConnectionString =
            new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = "postgres",
            }.ConnectionString;

        _databaseName = $"ocwip_test_{Guid.NewGuid():N}";

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;

        await using (var admin = new NpgsqlConnection(_maintenanceConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // Database names cannot be parameterized; Guid.N is alphanumeric only.
            create.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = connectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// A context built the same way the application builds its own, so no test
    /// can pass against settings the API never runs with.
    /// </summary>
    public AppDbContext CreateContext()
    {
        if (ConnectionString is null)
        {
            throw new InvalidOperationException(
                $"{RequiresDatabaseFactAttribute.Variable} is not set, so there " +
                "is no database. Guard the test with [RequiresDatabaseFact].");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(ConnectionString);
        return new AppDbContext(options.Options);
    }

    public async Task DisposeAsync()
    {
        if (_maintenanceConnectionString is null || _databaseName is null)
        {
            return;
        }

        await using var admin = new NpgsqlConnection(_maintenanceConnectionString);
        await admin.OpenAsync();

        // Terminate leftover sessions so DROP DATABASE does not hang.
        await using (var terminate = admin.CreateCommand())
        {
            terminate.CommandText =
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @name AND pid <> pg_backend_pid()
                """;
            terminate.Parameters.AddWithValue("name", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
        await drop.ExecuteNonQueryAsync();
    }
}
