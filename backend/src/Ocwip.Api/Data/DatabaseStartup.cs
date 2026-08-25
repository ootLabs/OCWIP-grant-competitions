using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ocwip.Api.Data;

/// <summary>
/// Applies pending migrations at startup, gated by Database:MigrateOnStartup.
///
/// On in development, so a fresh compose volume is usable after one command.
/// Off by default anywhere else: a process serving traffic should not hold DDL
/// rights, and /health has to answer even when the database does not.
/// See docs/architektura.md ("Migracje przy starcie").
/// </summary>
public static class DatabaseStartup
{
    private const int Attempts = 5;

    public static void ApplyPendingMigrations(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", false)
            || string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Postgres")))
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
                return;
            }
            // Only transient failures wait. A database restarted for a backup
            // is worth a retry; a broken migration is not, and reaches the
            // caller on the first attempt.
            catch (NpgsqlException exception) when (exception.IsTransient && attempt < Attempts)
            {
                app.Logger.LogWarning(
                    exception,
                    "Migration attempt {Attempt} of {Attempts} failed. Retrying in {Delay}.",
                    attempt,
                    Attempts,
                    delay);

                Thread.Sleep(delay);
                delay *= 2;
            }
        }
    }
}
