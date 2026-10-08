using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.EntityCards;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The Podmiot cards the caller acts for (T-93, T-93a). Applicant only, by
/// role policy. A card id in the route is honoured only for a card the caller
/// is a member of; any other id answers 404, the same as an id that does not
/// exist.
/// </summary>
public static class EntityCardEndpoints
{
    internal const string NotFound = "Nie ma takiej karty wśród Twoich podmiotów.";

    internal const string NipTaken =
        "Ta organizacja jest już zarejestrowana. Możesz poprosić o dostęp do jej karty; "
        + "prośbę zatwierdza osoba, która kartę założyła.";

    public static void MapEntityCardEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);

        app.MapGet("/me/entities",
            async Task<Results<Ok<IReadOnlyList<EntityCardSummary>>, ProblemHttpResult>> (
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(await cards.ListAsync(context.User, cancellationToken));
        })
            .WithName("ListMyEntityCards")
            .WithSummary("Every Podmiot card the caller acts for; empty before the first application.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/me/entities/{id:guid}",
            async Task<Results<Ok<EntityCardResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await cards.GetAsync(context.User, id, cancellationToken);
            return result.Outcome is EntityCardOutcome.Succeeded
                ? TypedResults.Ok(result.Card!)
                : TypedResults.Problem(NotFound, statusCode: 404);
        })
            .WithName("GetMyEntityCard")
            .WithSummary("One of the caller's Podmiot cards, with everybody who has access to it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapPost("/me/entities",
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
                EntityCardOutcome.Succeeded => TypedResults.Created($"/me/entities/{result.Card!.Id}", result.Card!),
                EntityCardOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                EntityCardOutcome.NipTaken => TypedResults.Problem(NipTaken, statusCode: 409),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("CreateMyEntityCard")
            .WithSummary(
                "Founds a Podmiot card; the caller becomes its founder. 409 when a card "
                + "with this NIP exists: the caller asks to join it instead.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapPut("/me/entities/{id:guid}",
            async Task<Results<Ok<EntityCardResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid id,
            EntityCardData body,
            [FromServices] IEntityCardService? cards,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (cards is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await cards.UpdateAsync(context.User, id, body, cancellationToken);
            return result.Outcome switch
            {
                EntityCardOutcome.Succeeded => TypedResults.Ok(result.Card!),
                EntityCardOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                EntityCardOutcome.NipTaken => TypedResults.Problem(NipTaken, statusCode: 409),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("UpdateMyEntityCard")
            .WithSummary(
                "Corrects one of the caller's Podmiot cards. Submitted applications keep "
                + "the copy taken when they were submitted.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);
    }
}
