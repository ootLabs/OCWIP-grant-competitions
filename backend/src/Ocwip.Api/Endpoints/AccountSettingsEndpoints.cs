using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// "Moje konto" (T-106): the signed in person changes their own password and
/// address; the link in the mail to the new address confirms the change.
/// Every route here is on the sensitive rate limit, like the rest of the
/// credential paths (T-12.5).
/// </summary>
public static class AccountSettingsEndpoints
{
    internal const string Unavailable = "Zmiana danych konta jest chwilowo niedostępna.";

    public static void MapAccountSettingsEndpoints(this WebApplication app)
    {
        app.MapPost("/me/password", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
            ChangePasswordRequest request,
            HttpContext context,
            [FromServices] IAccountSettingsService? settings,
            CancellationToken cancellationToken) =>
        {
            if (settings is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await settings.ChangePasswordAsync(context.User, request.CurrentPassword, request.NewPassword, cancellationToken);
            return Answer(result);
        })
            .WithName("ChangePassword")
            .WithSummary("Changes the signed in account's password; needs the current one and ends every other session.")
            .RequireRateLimiting(RateLimitingConfiguration.SensitivePolicy)
            .RequireAuthorization();

        app.MapPost("/me/email", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
            ChangeEmailRequest request,
            HttpContext context,
            [FromServices] IAccountSettingsService? settings,
            CancellationToken cancellationToken) =>
        {
            if (settings is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await settings.RequestEmailChangeAsync(context.User, request.NewEmail, request.CurrentPassword, cancellationToken);
            return Answer(result);
        })
            .WithName("RequestEmailChange")
            .WithSummary(
                "Starts a change of the account's address: a link to the new address, a notice to the old one. "
                + "Answers the same whether or not the new address already has an account.")
            .RequireRateLimiting(RateLimitingConfiguration.SensitivePolicy)
            .RequireAuthorization();

        app.MapPost("/confirm-email-change", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
            ConfirmEmailChangeRequest request,
            [FromServices] IAccountSettingsService? settings,
            CancellationToken cancellationToken) =>
        {
            if (settings is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await settings.ConfirmEmailChangeAsync(request.UserId, request.Email, request.Token, cancellationToken);
            return Answer(result);
        })
            .WithName("ConfirmEmailChange")
            .WithSummary("Completes a change of address from the link in the mail; every session of the account ends.")
            .RequireRateLimiting(RateLimitingConfiguration.SensitivePolicy)
            // The token in the link is the credential; the new address may be
            // opened on a device where nobody is signed in.
            .AllowAnonymous();
    }

    private static Results<NoContent, ValidationProblem, ProblemHttpResult> Answer(AccountSettingsResult result) =>
        result.Outcome switch
        {
            AccountSettingsOutcome.Succeeded => TypedResults.NoContent(),
            AccountSettingsOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
            AccountSettingsOutcome.InvalidToken => TypedResults.Problem(
                "Link do zmiany adresu jest nieprawidłowy lub wygasł. Poproś o zmianę jeszcze raz.", statusCode: 400),
            _ => TypedResults.Problem("Zaloguj się ponownie.", statusCode: 401),
        };
}
