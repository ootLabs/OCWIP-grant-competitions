using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Password recovery (T-12.4): "forgot my password" and the link it sends.
/// </summary>
public static class PasswordResetEndpoints
{
    public static void MapPasswordResetEndpoints(this WebApplication app)
    {
        app.MapPost("/forgot-password", async Task<Results<Ok, ProblemHttpResult>> (
            ForgotPasswordRequest request,
            // Same reasoning as AccountEndpoints.cs: IPasswordResetService is
            // only registered when a database is configured.
            [FromServices] IPasswordResetService? passwordReset,
            CancellationToken cancellationToken) =>
        {
            if (passwordReset is null)
            {
                return TypedResults.Problem(
                    "Reset hasła jest chwilowo niedostępny.", statusCode: 503);
            }

            // Always 200, whether or not an account exists for this address -
            // see IPasswordResetService.RequestResetAsync. The response must
            // never let a caller tell those cases apart.
            await passwordReset.RequestResetAsync(request.Email, cancellationToken);

            return TypedResults.Ok();
        })
            .WithName("ForgotPassword")
            .WithSummary(
                "Requests a password reset email. Answers the same for a "
                + "known and an unknown address, on purpose.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/reset-password", async Task<Results<Ok, ValidationProblem, ProblemHttpResult>> (
            ResetPasswordRequest request,
            [FromServices] IPasswordResetService? passwordReset,
            CancellationToken cancellationToken) =>
        {
            if (passwordReset is null)
            {
                return TypedResults.Problem(
                    "Reset hasła jest chwilowo niedostępny.", statusCode: 503);
            }

            var result = await passwordReset.ResetAsync(
                request.UserId, request.Token, request.NewPassword, cancellationToken);

            return result.Outcome switch
            {
                PasswordResetOutcome.Succeeded => TypedResults.Ok(),

                // One message for every reason the link itself did not work:
                // unknown account, malformed token, expired token, a token
                // already used once. See PasswordResetOutcome.InvalidToken.
                PasswordResetOutcome.InvalidToken => TypedResults.Problem(
                    "Link do resetowania hasła jest nieprawidłowy lub wygasł. "
                    + "Poproś o nowy.",
                    statusCode: 400),

                // Only the password policy reaches this point, in the same
                // shape /register uses for the same reason.
                _ => TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["newPassword"] = [.. result.Errors],
                }),
            };
        })
            .WithName("ResetPassword")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
