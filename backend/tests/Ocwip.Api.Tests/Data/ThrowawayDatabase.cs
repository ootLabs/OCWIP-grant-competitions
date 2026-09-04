using Npgsql;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// A PostgreSQL database created for a single test and dropped afterwards.
///
/// PostgresDatabaseFixture already does this once per test class and hands out a
/// migrated AppDbContext, which is what a test about the schema wants. A test
/// about a MIGRATION cannot use it: it has to see the database before the
/// migration ran, so it needs one of its own that nothing has migrated yet.
/// </summary>
internal sealed class ThrowawayDatabase : IAsyncDisposable
{
    private readonly string _maintenanceConnectionString;
    private readonly string _databaseName;

    private ThrowawayDatabase(
        string maintenanceConnectionString,
        string databaseName,
        string connectionString)
    {
        _maintenanceConnectionString = maintenanceConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    /// <summary>
    /// Call from a test guarded by [RequiresDatabaseFact]: without a connection
    /// string this throws rather than inventing an address, the same way the
    /// design time factory does.
    /// </summary>
    public static async Task<ThrowawayDatabase> CreateAsync(string prefix)
    {
        var baseConnectionString = RequiresDatabaseFactAttribute.ConnectionString;
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                $"{RequiresDatabaseFactAttribute.Variable} is not set, so there "
                + "is no database. Guard the test with [RequiresDatabaseFact].");
        }

        var maintenanceConnectionString =
            new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = "postgres",
            }.ConnectionString;

        var databaseName = $"ocwip_{prefix}_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;

        await using (var admin = new NpgsqlConnection(maintenanceConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // Database names cannot be parameterized; Guid.N is alphanumeric
            // only and the prefix comes from the test, never from data.
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync();
        }

        return new ThrowawayDatabase(
            maintenanceConnectionString,
            databaseName,
            connectionString);
    }

    public async ValueTask DisposeAsync()
    {
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
