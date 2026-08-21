using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Data;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// Proves that the migration chain applies to an empty database.
/// </summary>
public class MigrationTests
{
    [RequiresDatabaseFact]
    public async Task Migrations_apply_to_a_clean_database()
    {
        // Never null: the attribute skips the test when it is not configured.
        var baseConnectionString = RequiresDatabaseFactAttribute.ConnectionString!;

        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres",
        };
        var testDatabase = $"ocwip_mig_{Guid.NewGuid():N}";
        var testConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = testDatabase,
        }.ConnectionString;

        await using (var admin = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // Database names cannot be parameterized; Guid.N is alphanumeric only.
            create.CommandText = $"CREATE DATABASE \"{testDatabase}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            // The same configuration the application and dotnet ef use, so this
            // test cannot pass against settings the API never runs with.
            var options = new DbContextOptionsBuilder<AppDbContext>();
            options.UseOcwipPostgres(testConnectionString);

            await using var context = new AppDbContext(options.Options);
            await context.Database.MigrateAsync();

            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await using var admin = new NpgsqlConnection(maintenance.ConnectionString);
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
                terminate.Parameters.AddWithValue("name", testDatabase);
                await terminate.ExecuteNonQueryAsync();
            }

            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{testDatabase}\"";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
