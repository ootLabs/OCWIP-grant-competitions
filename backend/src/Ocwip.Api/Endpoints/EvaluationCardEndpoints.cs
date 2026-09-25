using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Versions of the two evaluation cards of a competition (T-38). The same
/// versioning as the application form (FormDefinitionEndpoints, T-25), with the
/// stage in the address: "formal" or "merit". Operator only, like the form.
/// </summary>
public static class EvaluationCardEndpoints
{
    internal const string UnknownStage =
        "Nie ma takiego etapu oceny. Dostępne: formal, merit.";

    public static void MapEvaluationCardEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/competitions/{competitionId:guid}/evaluation-cards/{stage}",
            async Task<Results<
                Created<FormDefinitionResponse>,
                ValidationProblem,
                ProblemHttpResult>> (
            Guid competitionId,
            string stage,
            FormDefinitionRequest request,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            if (Purpose(stage) is not { } purpose)
            {
                return TypedResults.Problem(UnknownStage, statusCode: 404);
            }

            var result = await definitions.PublishAsync(
                competitionId, purpose, request, cancellationToken);

            if (result.Outcome is FormDefinitionOutcome.InvalidDefinition)
            {
                return TypedResults.ValidationProblem(FormDefinitionEndpoints.Problems(result.Errors!));
            }

            if (result.Outcome is not FormDefinitionOutcome.Succeeded)
            {
                return FormDefinitionEndpoints.Failure(result);
            }

            var published = result.Definition!;
            return TypedResults.Created(
                $"/competitions/{competitionId}/evaluation-cards/{stage}/{published.VersionNumber}",
                published);
        })
            .WithName("PublishEvaluationCard")
            .WithSummary(
                "Publishes the next version of the formal or merit evaluation card. "
                + "Evaluations already started keep the version they were filled in on.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/evaluation-cards/{stage}",
            async Task<Results<
                Ok<IReadOnlyList<FormDefinitionSummaryResponse>>,
                ProblemHttpResult>> (
            Guid competitionId,
            string stage,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            if (Purpose(stage) is not { } purpose)
            {
                return TypedResults.Problem(UnknownStage, statusCode: 404);
            }

            var versions = await definitions.ListAsync(competitionId, purpose, cancellationToken);

            return versions is null
                ? TypedResults.Problem(FormDefinitionEndpoints.CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(versions);
        })
            .WithName("ListEvaluationCards")
            .WithSummary("Every published version of one evaluation card, oldest first.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/evaluation-cards/{stage}/{version:int}",
            async Task<Results<Ok<FormDefinitionResponse>, ProblemHttpResult>> (
            Guid competitionId,
            string stage,
            int version,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            if (Purpose(stage) is not { } purpose)
            {
                return TypedResults.Problem(UnknownStage, statusCode: 404);
            }

            var result = await definitions.GetAsync(competitionId, purpose, version, cancellationToken);

            return result.Outcome is FormDefinitionOutcome.Succeeded
                ? TypedResults.Ok(result.Definition!)
                : FormDefinitionEndpoints.Failure(result);
        })
            .WithName("GetEvaluationCard")
            .WithSummary("One version of an evaluation card with its document.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }

    /// <summary>The address says "formal" or "merit", lower case, and nothing else.</summary>
    internal static FormPurpose? Purpose(string stage) =>
        stage switch
        {
            "formal" => FormPurpose.FormalEvaluation,
            "merit" => FormPurpose.MeritEvaluation,
            _ => null,
        };
}
