using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The operator's grant decisions (T-42): an amount and a note per
/// application while the results are a draft, then one approval for the whole
/// competition that writes every result status at once.
/// </summary>
public static class GrantDecisionEndpoints
{
    internal const string NotFound = "Nie ma takiego złożonego wniosku.";

    internal const string ResultsApproved =
        "Wyniki tego konkursu są już zatwierdzone. Kwot i decyzji nie można już zmieniać.";

    public static void MapGrantDecisionEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPut("/applications/{applicationId:guid}/grant-decision",
            async Task<Results<Ok<GrantDecisionResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid applicationId,
            GrantDecisionRequest request,
            [FromServices] IGrantDecisionService? decisions,
            CancellationToken cancellationToken) =>
        {
            if (decisions is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await decisions.DecideAsync(applicationId, request, cancellationToken);
            return result.Outcome switch
            {
                GrantDecisionOutcome.Succeeded => TypedResults.Ok(result.Decision!),
                GrantDecisionOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                GrantDecisionOutcome.ResultsApproved => TypedResults.Problem(ResultsApproved, statusCode: 409),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("SetGrantDecision")
            .WithSummary("The awarded amount (null for none) and the note of one application, while the results are a draft.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{competitionId:guid}/results/approve",
            async Task<Results<Ok<ResultsApprovalResponse>, ProblemHttpResult>> (
            Guid competitionId,
            HttpContext context,
            [FromServices] IGrantDecisionService? decisions,
            CancellationToken cancellationToken) =>
        {
            if (decisions is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var operatorId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await decisions.ApproveAsync(competitionId, operatorId, cancellationToken);
            return result.Outcome switch
            {
                GrantDecisionOutcome.Succeeded => TypedResults.Ok(result.Approval!),
                GrantDecisionOutcome.ResultsApproved => TypedResults.Problem(ResultsApproved, statusCode: 409),
                GrantDecisionOutcome.EvaluationUnfinished => TypedResults.Problem(
                    $"Nie wszystkie wnioski mają zakończoną ocenę (czeka: {result.Unfinished}). "
                    + "Wynik można zatwierdzić dopiero po ocenie formalnej i komplecie kart merytorycznych.",
                    statusCode: 409),
                _ => TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404),
            };
        })
            .WithName("ApproveResults")
            .WithSummary("Approves the results of a competition once: every application gets its result status at the same moment.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);
    }
}
