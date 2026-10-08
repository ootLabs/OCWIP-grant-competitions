using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.EntityCards;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Joining a Podmiot card that already exists (T-93a, report step 2.2): the
/// applicant asks by NIP, the founder decides, and after seven days without
/// an answer an operator decides with a note on how the person was checked.
/// </summary>
public static class EntityAccessRequestEndpoints
{
    internal const string InvalidNip = "Podaj poprawny NIP organizacji.";
    internal const string NoCard =
        "Nie ma karty organizacji z tym NIP-em. Możesz ją założyć przy pierwszym wniosku.";
    internal const string AlreadyMember = "Masz już dostęp do karty tej organizacji.";
    internal const string NotFound = "Nie ma takiej prośby.";
    internal const string NotFounder = "Prośby o dostęp rozpatruje osoba, która założyła kartę.";
    internal const string AlreadyDecided = "Ta prośba została już rozpatrzona.";
    internal const string NotEscalated =
        "Operator rozpatruje prośbę dopiero po 7 dniach bez odpowiedzi osoby, która założyła kartę.";
    internal const string OperatorNoteRequired =
        "Zapisz, jak sprawdzono osobę proszącą o dostęp (najwyżej 1000 znaków).";
    internal const string FounderNoNote = "Decyzja osoby, która założyła kartę, nie ma notatki.";

    public static void MapEntityAccessRequestEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/me/access-requests",
            async Task<Results<Ok<MyEntityAccessRequest>, ProblemHttpResult>> (
            EntityAccessRequestBody body,
            [FromServices] IEntityAccessRequestService? requests,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var result = await requests.RequestAsync(context.User, body.Nip, cancellationToken);
            return result.Outcome switch
            {
                EntityAccessOutcome.Succeeded => TypedResults.Ok(result.Request!),
                EntityAccessOutcome.InvalidNip => TypedResults.Problem(InvalidNip, statusCode: 400),
                EntityAccessOutcome.AlreadyMember => TypedResults.Problem(AlreadyMember, statusCode: 409),
                _ => TypedResults.Problem(NoCard, statusCode: 404),
            };
        })
            .WithName("RequestEntityAccess")
            .WithSummary(
                "Asks the founder of the card with this NIP for access. Asking again "
                + "returns the open request instead of a second one.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/me/access-requests",
            async Task<Results<Ok<IReadOnlyList<MyEntityAccessRequest>>, ProblemHttpResult>> (
            [FromServices] IEntityAccessRequestService? requests,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(await requests.ListMineAsync(context.User, cancellationToken));
        })
            .WithName("ListMyEntityAccessRequests")
            .WithSummary("The caller's own requests to join a card, newest first.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/me/entities/{id:guid}/access-requests",
            async Task<Results<Ok<IReadOnlyList<PendingEntityAccessRequest>>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IEntityAccessRequestService? requests,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var (outcome, pending) = await requests.ListPendingAsync(context.User, id, cancellationToken);
            return outcome switch
            {
                EntityAccessOutcome.Succeeded => TypedResults.Ok(pending),
                EntityAccessOutcome.NotFounder => TypedResults.Problem(NotFounder, statusCode: 403),
                _ => TypedResults.Problem(EntityCardEndpoints.NotFound, statusCode: 404),
            };
        })
            .WithName("ListPendingEntityAccessRequests")
            .WithSummary("Requests waiting for the founder of this card. Founder only.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapPost("/me/entities/{id:guid}/access-requests/{requestId:guid}/decision",
            async Task<Results<NoContent, ProblemHttpResult>> (
            Guid id,
            Guid requestId,
            EntityAccessDecisionBody body,
            [FromServices] IEntityAccessRequestService? requests,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var outcome = await requests.DecideAsFounderAsync(context.User, id, requestId, body, cancellationToken);
            return outcome switch
            {
                EntityAccessOutcome.Succeeded => TypedResults.NoContent(),
                EntityAccessOutcome.NotFounder => TypedResults.Problem(NotFounder, statusCode: 403),
                EntityAccessOutcome.AlreadyDecided => TypedResults.Problem(AlreadyDecided, statusCode: 409),
                EntityAccessOutcome.NoteMismatch => TypedResults.Problem(FounderNoNote, statusCode: 400),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("DecideEntityAccessRequest")
            .WithSummary("The founder approves or refuses a request to join the card.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/access-requests/escalated",
            async Task<Results<Ok<IReadOnlyList<EscalatedEntityAccessRequest>>, ProblemHttpResult>> (
            [FromServices] IEntityAccessRequestService? requests,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(await requests.ListEscalatedAsync(cancellationToken));
        })
            .WithName("ListEscalatedEntityAccessRequests")
            .WithSummary("Requests nobody answered for seven days, oldest first. Operator only.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/access-requests/{requestId:guid}/decision",
            async Task<Results<NoContent, ProblemHttpResult>> (
            Guid requestId,
            EntityAccessDecisionBody body,
            [FromServices] IEntityAccessRequestService? requests,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (requests is null)
            {
                return TypedResults.Problem(ApplicationOverviewEndpoints.Unavailable, statusCode: 503);
            }

            var outcome = await requests.DecideAsOperatorAsync(context.User, requestId, body, cancellationToken);
            return outcome switch
            {
                EntityAccessOutcome.Succeeded => TypedResults.NoContent(),
                EntityAccessOutcome.NoteMismatch => TypedResults.Problem(OperatorNoteRequired, statusCode: 400),
                EntityAccessOutcome.AlreadyDecided => TypedResults.Problem(AlreadyDecided, statusCode: 409),
                EntityAccessOutcome.NotEscalated => TypedResults.Problem(NotEscalated, statusCode: 409),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("DecideEscalatedEntityAccessRequest")
            .WithSummary(
                "An operator approves or refuses a request nobody answered for seven days, "
                + "with a note on how the person was checked. Operator only.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }
}
