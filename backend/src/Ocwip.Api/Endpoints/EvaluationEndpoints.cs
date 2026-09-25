using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Evaluations of applications over HTTP (T-38). Two ways in, one per stage,
/// each behind a fixed role policy: the operator starts the formal card, an
/// expert the merit one, and only on an application assigned to them (the
/// resource policy of T-37). Everything after that is on the evaluation
/// itself and goes through EvaluationAccessHandler.
/// </summary>
public static class EvaluationEndpoints
{
    internal const string Unavailable = "Ocena wniosków jest chwilowo niedostępna.";
    internal const string ApplicationNotFound = "Nie ma takiego wniosku.";
    internal const string ForbiddenApplication = "Ten wniosek nie jest przypisany do Twojej oceny.";
    internal const string NotSubmitted = "Ocenia się tylko złożone wnioski.";
    internal const string NoCard = "Konkurs nie ma jeszcze opublikowanej karty oceny dla tego etapu.";
    internal const string NotFound = "Nie ma takiej oceny.";
    internal const string Forbidden = "Nie masz dostępu do tej oceny.";
    internal const string AlreadyFinished = "Ten etap oceny został już zakończony.";

    public static void MapEvaluationEndpoints(this WebApplication app)
    {
        app.MapPost("/applications/{applicationId:guid}/evaluations/formal",
            async Task<Results<Created<EvaluationResponse>, Ok<EvaluationResponse>, ProblemHttpResult>> (
            Guid applicationId,
            HttpContext context,
            [FromServices] IEvaluationService? evaluations,
            CancellationToken cancellationToken) =>
        {
            if (evaluations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return Started(await evaluations.StartAsync(
                applicationId, EvaluationStage.Formal, CallerId(context), cancellationToken));
        })
            .WithName("StartFormalEvaluation")
            .WithSummary("Opens the formal evaluation card of a submitted application, one per application.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Operator));

        app.MapPost("/applications/{applicationId:guid}/evaluations/merit",
            async Task<Results<Created<EvaluationResponse>, Ok<EvaluationResponse>, ProblemHttpResult>> (
            Guid applicationId,
            HttpContext context,
            [FromServices] IEvaluationService? evaluations,
            [FromServices] IApplicationService? applications,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (evaluations is null || applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            // The same load, ask, answer as every application route: for an
            // expert the resource policy passes only on an assigned
            // application, and that assignment is the whole right to score it.
            var resource = await applications.FindForAuthorizationAsync(applicationId, cancellationToken);

            if (resource is null)
            {
                return TypedResults.Problem(ApplicationNotFound, statusCode: 404);
            }

            var allowed = await authorization.AuthorizeAsync(
                context.User, resource, AuthorizationConfiguration.Names.OwnsResource);

            if (!allowed.Succeeded)
            {
                return TypedResults.Problem(ForbiddenApplication, statusCode: 403);
            }

            return Started(await evaluations.StartAsync(
                applicationId, EvaluationStage.Merit, CallerId(context), cancellationToken));
        })
            .WithName("StartMeritEvaluation")
            .WithSummary("Opens the caller's own merit evaluation card of an application assigned to them.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Reviewer));

        app.MapGet("/evaluations/{evaluationId:guid}",
            async Task<Results<Ok<EvaluationResponse>, ProblemHttpResult>> (
            Guid evaluationId,
            HttpContext context,
            [FromServices] IEvaluationService? evaluations,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (evaluations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await AuthorizeAsync(evaluations, authorization, context, evaluationId,
                    AuthorizationConfiguration.Names.ReadsEvaluation, cancellationToken) is { } problem)
            {
                return problem;
            }

            return Answer(await evaluations.GetAsync(evaluationId, cancellationToken));
        })
            .WithName("GetEvaluation")
            .WithSummary("One evaluation with its card and the result read from its answers.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapPut("/evaluations/{evaluationId:guid}",
            async Task<Results<Ok<EvaluationResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid evaluationId,
            SaveEvaluationRequest request,
            HttpContext context,
            [FromServices] IEvaluationService? evaluations,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (evaluations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await AuthorizeAsync(evaluations, authorization, context, evaluationId,
                    AuthorizationConfiguration.Names.WritesEvaluation, cancellationToken) is { } problem)
            {
                return problem;
            }

            var result = await evaluations.SaveAsync(evaluationId, request, cancellationToken);

            if (result.Outcome is EvaluationOutcome.AnswersRejected)
            {
                return TypedResults.ValidationProblem(result.Errors!);
            }

            return result.Outcome is EvaluationOutcome.Succeeded
                ? TypedResults.Ok(result.Evaluation!)
                : Failure(result);
        })
            .WithName("SaveEvaluation")
            .WithSummary("Autosave of an evaluation draft: refuses only what the card could not have sent.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapPost("/evaluations/{evaluationId:guid}/finish",
            async Task<Results<Ok<EvaluationResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid evaluationId,
            HttpContext context,
            [FromServices] IEvaluationService? evaluations,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (evaluations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await AuthorizeAsync(evaluations, authorization, context, evaluationId,
                    AuthorizationConfiguration.Names.WritesEvaluation, cancellationToken) is { } problem)
            {
                return problem;
            }

            var result = await evaluations.FinishAsync(evaluationId, cancellationToken);

            if (result.Outcome is EvaluationOutcome.AnswersRejected)
            {
                return TypedResults.ValidationProblem(result.Errors!);
            }

            return result.Outcome is EvaluationOutcome.Succeeded
                ? TypedResults.Ok(result.Evaluation!)
                : Failure(result);
        })
            .WithName("FinishEvaluation")
            .WithSummary("\"Zapisz i zakończ etap\": checks the whole card and closes it for editing.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }

    /// <summary>The account id Identity writes into the cookie's claims.</summary>
    private static Guid CallerId(HttpContext context) =>
        Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static async Task<ProblemHttpResult?> AuthorizeAsync(
        IEvaluationService evaluations,
        IAuthorizationService authorization,
        HttpContext context,
        Guid evaluationId,
        string policy,
        CancellationToken cancellationToken)
    {
        var evaluation = await evaluations.FindForAuthorizationAsync(evaluationId, cancellationToken);

        if (evaluation is null)
        {
            return TypedResults.Problem(NotFound, statusCode: 404);
        }

        var allowed = await authorization.AuthorizeAsync(context.User, evaluation, policy);

        return allowed.Succeeded ? null : TypedResults.Problem(Forbidden, statusCode: 403);
    }

    private static Results<Created<EvaluationResponse>, Ok<EvaluationResponse>, ProblemHttpResult> Started(
        EvaluationResult result) =>
        result.Outcome switch
        {
            EvaluationOutcome.Created => TypedResults.Created(
                $"/evaluations/{result.Evaluation!.Id}", result.Evaluation),
            EvaluationOutcome.Succeeded => TypedResults.Ok(result.Evaluation!),
            _ => Failure(result),
        };

    private static Results<Ok<EvaluationResponse>, ProblemHttpResult> Answer(EvaluationResult result) =>
        result.Outcome is EvaluationOutcome.Succeeded
            ? TypedResults.Ok(result.Evaluation!)
            : Failure(result);

    private static ProblemHttpResult Failure(EvaluationResult result) =>
        result.Outcome switch
        {
            EvaluationOutcome.ApplicationNotFound => TypedResults.Problem(ApplicationNotFound, statusCode: 404),
            EvaluationOutcome.NotFound => TypedResults.Problem(NotFound, statusCode: 404),
            EvaluationOutcome.NotSubmitted => TypedResults.Problem(NotSubmitted, statusCode: 409),
            EvaluationOutcome.NoCard => TypedResults.Problem(NoCard, statusCode: 409),
            EvaluationOutcome.AlreadyFinished => TypedResults.Problem(AlreadyFinished, statusCode: 409),
            // AnswersRejected is answered at its routes with the fields named;
            // an outcome added later arrives as a visible 500.
            _ => throw new InvalidOperationException($"Unhandled evaluation outcome: {result.Outcome}"),
        };
}
