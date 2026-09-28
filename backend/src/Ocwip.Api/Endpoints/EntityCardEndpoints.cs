using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.EntityCards;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The caller's own Podmiot card (T-93). Applicant only, by role policy, and
/// always the caller's own: the route carries no entity id, so there is no
/// other organisation's card to name.
/// </summary>
public static class EntityCardEndpoints
{
    internal const string NotFound = "Nie masz jeszcze danych wnioskodawcy. Uzupełnisz je przy pierwszym wniosku.";
    internal const string AlreadyExists = "Dane wnioskodawcy są już zapisane. Popraw je zamiast zakładać nowe.";

    public static void MapEntityCardEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);

        app.MapGet("/me/entity",
            async Task<Results<Ok<EntityCardResponse>, ProblemHttpResult>> (
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await cards.GetAsync(context.User, cancellationToken);
            return result.Outcome is EntityCardOutcome.Succeeded
                ? TypedResults.Ok(result.Card!)
                : TypedResults.Problem(NotFound, statusCode: 404);
        })
            .WithName("GetMyEntityCard")
            .WithSummary("The caller's own Podmiot card, or 404 before the first application.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapPost("/me/entity",
            async Task<Results<Created<EntityCardResponse>, ValidationProblem, ProblemHttpResult>> (
            EntityCardData body,
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await cards.CreateAsync(context.User, body, cancellationToken);
            return result.Outcome switch
            {
                EntityCardOutcome.Succeeded => TypedResults.Created("/me/entity", result.Card!),
                EntityCardOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                _ => TypedResults.Problem(AlreadyExists, statusCode: 409),
            };
        })
            .WithName("CreateMyEntityCard")
            .WithSummary("Creates the caller's Podmiot card; the caller becomes its account.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapPut("/me/entity",
            async Task<Results<Ok<EntityCardResponse>, ValidationProblem, ProblemHttpResult>> (
            EntityCardData body,
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await cards.UpdateAsync(context.User, body, cancellationToken);
            return result.Outcome switch
            {
                EntityCardOutcome.Succeeded => TypedResults.Ok(result.Card!),
                EntityCardOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("UpdateMyEntityCard")
            .WithSummary(
                "Corrects the caller's Podmiot card. Submitted applications keep "
                + "the copy taken when they were submitted.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);
    }
}
