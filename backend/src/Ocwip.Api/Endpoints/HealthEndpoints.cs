using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;

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
            .WithSummary("Liveness probe. Says nothing about the database.")
            // Public by decision, not by omission (T-13.2): the container
            // healthcheck and scripts/smoke_test.py call this without a
            // session, and a liveness probe that needs a login cannot report
            // that the application is down.
            .AllowAnonymous();

        app.MapGet("/health/db", async Task<Results<Ok<DatabaseHealthResponse>, ProblemHttpResult>> (
            [FromServices] DatabaseProbe probe,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (probe.Source is not { } source)
                {
                    return TypedResults.Problem(
                        "Connection string 'Postgres' is not configured.", statusCode: 503);
                }

                await using var command = source.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return TypedResults.Ok(new DatabaseHealthResponse("ok", "reachable"));
            }
            // Any failure, not only NpgsqlException (T-111): a malformed
            // connection string throws ArgumentException, and a probe that
            // answers 500 with a stack trace is not a probe.
            catch (Exception exception) when (exception is not OperationCanceledException)
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
            // ProducesProblem keeps the declared media type equal to the one
            // actually sent, application/problem+json.
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            // Same reason as /health. The body already names neither the host
            // nor the credentials, see the 503 above.
            .AllowAnonymous();
    }
}
