using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Account registration (T-12.1). Verifying the address is T-12.2 and signing
/// in is T-12.3, so neither happens here.
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/register", async (
            RegisterRequest request,
            // Explicit, because IAccountService is only registered when a
            // database is configured (see Program.cs). Without [FromServices],
            // minimal APIs decide a parameter's binding source by checking
            // whether the type is a registered service at endpoint-build time,
            // which happens once for every endpoint on the first request to
            // any of them. On a host with no database that check fails, and it
            // takes down routing for the whole app, not just this endpoint.
            //
            // Nullable, so the binder asks GetService rather than
            // GetRequiredService: see the 503 below.
            [FromServices] IAccountService? accounts,
            CancellationToken cancellationToken) =>
        {
            if (accounts is null)
            {
                // This host was started with no ConnectionStrings:Postgres, so
                // Identity's store and this service were never registered.
                // That is a supported way to run the API rather than a
                // misconfiguration, because the health probes have to answer
                // without a database, and 503 is the honest answer for a write
                // path in that state. GetRequiredService would raise a 500
                // whose body names an internal type, which is the opposite of
                // what /health/db is careful to avoid.
                return Results.Problem(
                    "Rejestracja jest chwilowo niedostępna.", statusCode: 503);
            }

            // Before anything reads the request: the trimmed value is both what
            // gets validated and what gets stored, see the validator.
            request = RegisterRequestValidator.Trim(request);

            var problems = RegisterRequestValidator.Validate(request);
            if (problems.Count > 0)
            {
                return Results.ValidationProblem(problems);
            }

            var result = await accounts.RegisterAsync(
                request, cancellationToken);

            if (result.Outcome is RegistrationOutcome.Accepted)
            {
                // 202 with an EMPTY body, byte for byte the same for a free and
                // for a taken address. Not 201: nothing was created in the
                // taken case, and Created also promises a Location we would
                // have to invent. What happens next arrives by mail (T-12.2),
                // which is the only channel allowed to know which case it was.
                return Results.Accepted();
            }

            // Only the password policy reaches this point. Duplicate errors were
            // dropped in the service, so nothing here can reveal whether an
            // account already exists, and the address was refused above against
            // the same rule Identity applies, so Identity's own InvalidEmail
            // cannot arrive here and be reported as a password problem.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = [.. result.Errors],
            });
        })
            .WithName("RegisterAccount")
            .WithSummary(
                "Registers an applicant account. Answers the same for a taken "
                + "and a free address, on purpose.");
    }
}
