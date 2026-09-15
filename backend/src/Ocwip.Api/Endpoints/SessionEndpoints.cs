using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Signing in, signing out and reading the current session (T-12.3).
///
/// Separate from AccountEndpoints, which owns creating an account and
/// confirming an address. Same reason the services are separate: a session has
/// a different lifetime from an account, and both files stay small enough to
/// read in one go.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>
    /// One message for every credential failure, and the card says why:
    /// telling "no such account" apart from "wrong password" lets anyone check
    /// from outside who has an account here.
    /// </summary>
    internal const string InvalidCredentials =
        "Nieprawidłowy e-mail lub hasło.";

    internal const string EmailNotConfirmed =
        "Potwierdź swój adres e-mail, zanim się zalogujesz. "
        + "Link do potwierdzenia wysłaliśmy na ten adres przy zakładaniu konta.";

    internal const string Unavailable =
        "Logowanie jest chwilowo niedostępne.";

    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/login", async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> (
            LoginRequest request,
            // Explicit and nullable for the same reason as /register: the
            // service exists only when a connection string does, and letting
            // the binder decide by looking the type up at endpoint-build time
            // takes down routing for the whole app on a host without one. See
            // the long note in AccountEndpoints.
            [FromServices] ISessionService? sessions,
            CancellationToken cancellationToken) =>
        {
            if (sessions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await sessions.LoginAsync(request, cancellationToken);

            return result.Outcome switch
            {
                LoginOutcome.Succeeded => TypedResults.Ok(result.Session!),

                // 403, not 401: the credentials were right, so retrying them
                // changes nothing. 401 would tell the browser to ask for
                // credentials again, which is the one thing that cannot help
                // here. Reachable only past a correct password, see LoginOutcome.
                LoginOutcome.EmailNotConfirmed => TypedResults.Problem(
                    EmailNotConfirmed, statusCode: 403),

                _ => TypedResults.Problem(InvalidCredentials, statusCode: 401),
            };
        })
            .WithName("Login")
            .WithSummary(
                "Signs in with an e-mail and a password, and issues the session "
                + "cookie. Answers the same for an unknown address and a wrong "
                + "password, on purpose.")
            // ProblemHttpResult carries its status in a runtime argument, so
            // none of these can be read off the signature.
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/logout", async Task<Ok> (
            [FromServices] ISessionService? sessions,
            HttpContext context) =>
        {
            // No RequireAuthorization and no 401 branch. Logging out is
            // idempotent: a caller whose session already ended asked for the
            // state it is already in, and answering 401 there only teaches the
            // front to treat a successful logout as an error. A host with no
            // database has no session to end either, so it is the same answer.
            if (sessions is not null)
            {
                await sessions.LogoutAsync(context.User);
            }

            return TypedResults.Ok();
        })
            .WithName("Logout")
            .WithSummary(
                "Ends the session on the server, not only in this browser.");

        app.MapGet("/me", async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> (
            [FromServices] ISessionService? sessions,
            HttpContext context) =>
        {
            // The cookie was valid, and the account behind it is gone or
            // deactivated. RequireAuthorization below cannot see that, because
            // it reads the cookie and not the row.
            var user = sessions is null
                ? null
                : await sessions.CurrentUserAsync(context.User);

            return user is null
                ? TypedResults.Problem(
                    "Twoja sesja wygasła. Zaloguj się ponownie.",
                    statusCode: 401)
                : TypedResults.Ok(user);
        })
            .WithName("CurrentUser")
            .WithSummary("The account behind the current session cookie.")
            // Authentication only. Which role may reach WHAT is the subject of
            // T-13.2, and this endpoint is the one place every signed in role
            // is equally entitled to.
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
