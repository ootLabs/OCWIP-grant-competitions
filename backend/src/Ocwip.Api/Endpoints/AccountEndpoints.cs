using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapRegisterEndpoints(this WebApplication app)
    {
        app.MapPost("/register", async (
            RegisterRequest request,
            // Explicit, because IAccountService is only registered when a
            // database is configured (see Program.cs). Without [FromServices],
            // minimal APIs decide a parameter's binding source by checking
            // whether the type is a registered service at endpoint-build time,
            // which happens once for every endpoint on the first request to
            // any of them. On a host with no database that check fails, and
            // it takes down routing for the whole app, not just /register.
            [FromServices] IAccountService service) =>
        {
            var result = await service.RegisterAsync(request);

            if (result.Succeeded)
            {
                return Results.Created();
            }

            var errors = result.Errors
                .Select(error => new
                {
                    code = error.Code,
                    description = error.Description
                })
                .ToList();

            var duplicateEmail = result.Errors.Any(error =>
                error.Code == "DuplicateEmail" ||
                error.Code == "DuplicateUserName");

            if (duplicateEmail)
            {
                return Results.Conflict(new
                {
                    errors
                });
            }

            return Results.BadRequest(new
            {
                errors
            });
        })
        .WithName("RegisterUser");
    }

    public static void MapEmailVerificationEndpoints(this WebApplication app)
    {
        app.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            // Same reasoning as /register above: IEmailVerificationService is
            // only registered when a database is configured.
            [FromServices] IEmailVerificationService service) =>
        {
            var verified = await service.VerifyAsync(request.UserId, request.Token);

            if (!verified)
            {
                return Results.BadRequest(new
                {
                    error = "Nie udało się potwierdzić adresu e-mail. Link może być " +
                        "nieprawidłowy, wygasły, lub konto zostało już potwierdzone."
                });
            }

            return Results.Ok();
        })
        .WithName("VerifyEmail");

        app.MapPost("/resend-verification", async (
            ResendVerificationRequest request,
            [FromServices] IEmailVerificationService service) =>
        {
            // Always 200, whether or not an account exists for this address
            // and whether or not an email was actually sent (already
            // confirmed, or still within the resend cooldown) - see
            // IEmailVerificationService.ResendVerificationAsync. The response
            // must never let a caller tell those cases apart.
            await service.ResendVerificationAsync(request.Email);

            return Results.Ok();
        })
        .WithName("ResendVerification");
    }
}
