using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Ocwip.Api.Contracts;

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
        app.MapGet("/health", Ok<HealthResponse> () => TypedResults.Ok(new HealthResponse("ok")))
            .WithName("Health")
            .WithSummary("Liveness probe. Says nothing about the database.");

        app.MapGet("/health/db", async Task<Results<Ok<DatabaseHealthResponse>, ProblemHttpResult>> (
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var connectionString = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return TypedResults.Problem(
                    "Connection string 'Postgres' is not configured.", statusCode: 503);
            }

            try
            {
                await using var connection = new NpgsqlDataSourceBuilder(connectionString).Build();
                await using var command = connection.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return TypedResults.Ok(new DatabaseHealthResponse("ok", "reachable"));
            }
            catch (NpgsqlException exception)
            {
                // The message is deliberately generic: the exception text can
                // carry the host, the user and the database name.
                app.Logger.LogError(exception, "Database probe failed.");
                return TypedResults.Problem("Database is not reachable.", statusCode: 503);
            }
        })
            .WithName("HealthDatabase")
            .WithSummary("Checks that the API can reach PostgreSQL.")
            // 503 has to be declared by hand: ProblemHttpResult carries its
            // status in a runtime argument, so the signature cannot declare it.
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}
