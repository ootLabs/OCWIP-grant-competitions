using Npgsql;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Liveness and database probes. Kept separate from the database probe on
/// purpose: an orchestrator restarting the API because Postgres is briefly
/// unavailable turns a small outage into a large one.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health")
            .WithSummary("Liveness probe. Says nothing about the database.");

        app.MapGet("/health/db", async (IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var connectionString = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Results.Problem("Connection string 'Postgres' is not configured.", statusCode: 503);
            }

            try
            {
                await using var connection = new NpgsqlDataSourceBuilder(connectionString).Build();
                await using var command = connection.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return Results.Ok(new { status = "ok", database = "reachable" });
            }
            catch (NpgsqlException exception)
            {
                // The message is deliberately generic: the exception text can
                // carry the host, the user and the database name.
                app.Logger.LogError(exception, "Database probe failed.");
                return Results.Problem("Database is not reachable.", statusCode: 503);
            }
        })
            .WithName("HealthDatabase")
            .WithSummary("Checks that the API can reach PostgreSQL.");
    }
}
