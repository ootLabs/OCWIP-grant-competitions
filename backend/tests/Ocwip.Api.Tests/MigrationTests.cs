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
    [Fact]
    public async Task Migrations_apply_to_a_clean_database()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            // No database available: skip rather than fail (docs/testy.md).
            return;
        }

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
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(testConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options;

            await using var context = new AppDbContext(options);
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
