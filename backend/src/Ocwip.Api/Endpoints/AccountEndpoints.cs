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
}
