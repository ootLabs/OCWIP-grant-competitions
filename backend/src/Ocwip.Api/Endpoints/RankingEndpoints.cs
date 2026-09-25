using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The evaluation settings and the ranking list of a competition (T-39),
/// operator only. The settings have their own route rather than riding on
/// the competition request, because the announcement wizard sends the whole
/// competition on every save and would reset them each time.
/// </summary>
public static class RankingEndpoints
{
    internal const string Unavailable = "Lista rankingowa jest chwilowo niedostępna.";
    internal const string CompetitionNotFound = "Nie ma takiego konkursu.";
    internal const string Inactive = "Ten konkurs jest oznaczony jako nieaktywny, więc nie można zmieniać jego ustawień.";

    public static void MapRankingEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{competitionId:guid}/evaluation-settings",
            async Task<Results<Ok<EvaluationSettingsResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IRankingService? ranking,
            CancellationToken cancellationToken) =>
        {
            if (ranking is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await ranking.GetSettingsAsync(competitionId, cancellationToken);
            return result.Outcome is RankingOutcome.Succeeded
                ? TypedResults.Ok(result.Settings!)
                : Failure(result);
        })
            .WithName("GetEvaluationSettings")
            .WithSummary("How applications of a competition are evaluated: experts per application, sum or average, threshold.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPut("/competitions/{competitionId:guid}/evaluation-settings",
            async Task<Results<Ok<EvaluationSettingsResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid competitionId,
            EvaluationSettingsRequest request,
            [FromServices] IRankingService? ranking,
            CancellationToken cancellationToken) =>
        {
            if (ranking is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await ranking.UpdateSettingsAsync(competitionId, request, cancellationToken);

            if (result.Outcome is RankingOutcome.InvalidSettings)
            {
                return TypedResults.ValidationProblem(result.Errors!);
            }

            return result.Outcome is RankingOutcome.Succeeded
                ? TypedResults.Ok(result.Settings!)
                : Failure(result);
        })
            .WithName("UpdateEvaluationSettings")
            .WithSummary("Sets how applications of a competition are evaluated.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/ranking",
            async Task<Results<Ok<RankingResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IRankingService? ranking,
            CancellationToken cancellationToken) =>
        {
            if (ranking is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await ranking.GetRankingAsync(competitionId, cancellationToken);
            return result.Outcome is RankingOutcome.Succeeded
                ? TypedResults.Ok(result.Ranking!)
                : Failure(result);
        })
            .WithName("GetRanking")
            .WithSummary("The ranking list: ranked applications by score, ties to the earlier submission, then the rest.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);
    }

    /// <summary>
    /// The expert's own list (T-40), behind the reviewer role; what it holds is
    /// scoped by the caller's assignments inside the service.
    /// </summary>
    public static void MapReviewerWorkEndpoints(this WebApplication app)
    {
        app.MapGet("/reviewer/applications",
            async Task<Results<Ok<ReviewerWorkResponse>, ProblemHttpResult>> (
            HttpContext context,
            [FromServices] IReviewerWorkService? work,
            CancellationToken cancellationToken) =>
        {
            if (work is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var reviewerId = CallerId(context);
            return TypedResults.Ok(await work.ForAsync(reviewerId, cancellationToken));
        })
            .WithName("ListReviewerApplications")
            .WithSummary("The applications assigned to the calling expert, with their own card and the three sums.")
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Reviewer));
    }

    internal const string AlreadyDecided =
        "Deklaracja w tym konkursie została już złożona. Zmianę decyzji ustala operator OCWIP.";

    /// <summary>The impartiality declaration (T-40a): the expert's own, and the operator's overview.</summary>
    public static void MapDeclarationEndpoints(this WebApplication app)
    {
        var reviewerPolicy = AuthorizationConfiguration.Names.For(Role.Reviewer);

        app.MapGet("/reviewer/competitions/{competitionId:guid}/declaration",
            async Task<Results<Ok<DeclarationResponse>, ProblemHttpResult>> (
            Guid competitionId,
            HttpContext context,
            [FromServices] IDeclarationService? declarations,
            CancellationToken cancellationToken) =>
        {
            if (declarations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await declarations.GetAsync(competitionId, CallerId(context), cancellationToken);
            return result.Outcome is DeclarationOutcome.Succeeded
                ? TypedResults.Ok(result.Declaration!)
                : TypedResults.Problem(CompetitionNotFound, statusCode: 404);
        })
            .WithName("GetOwnDeclaration")
            .WithSummary("The calling expert's impartiality declaration for a competition, with its text.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(reviewerPolicy);

        app.MapPost("/reviewer/competitions/{competitionId:guid}/declaration",
            async Task<Results<Ok<DeclarationResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid competitionId,
            DeclarationDecisionRequest request,
            HttpContext context,
            [FromServices] IDeclarationService? declarations,
            CancellationToken cancellationToken) =>
        {
            if (declarations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await declarations.DecideAsync(competitionId, CallerId(context), request, cancellationToken);

            return result.Outcome switch
            {
                DeclarationOutcome.Succeeded => TypedResults.Ok(result.Declaration!),
                DeclarationOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                DeclarationOutcome.AlreadyDecided => TypedResults.Problem(AlreadyDecided, statusCode: 409),
                _ => TypedResults.Problem(CompetitionNotFound, statusCode: 404),
            };
        })
            .WithName("DecideDeclaration")
            .WithSummary("Accepts the impartiality declaration, or refuses it with a reason. Decided once.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(reviewerPolicy);

        app.MapGet("/competitions/{competitionId:guid}/declarations",
            async Task<Results<Ok<IReadOnlyList<DeclarationRow>>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IDeclarationService? declarations,
            CancellationToken cancellationToken) =>
        {
            if (declarations is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var rows = await declarations.ListAsync(competitionId, cancellationToken);
            return rows is null
                ? TypedResults.Problem(CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(rows);
        })
            .WithName("ListDeclarations")
            .WithSummary("Every expert assigned in a competition with the state of their impartiality declaration.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Operator));
    }

    private static Guid CallerId(HttpContext context) =>
        Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static ProblemHttpResult Failure(RankingResult result) =>
        result.Outcome switch
        {
            RankingOutcome.CompetitionNotFound => TypedResults.Problem(CompetitionNotFound, statusCode: 404),
            RankingOutcome.Inactive => TypedResults.Problem(Inactive, statusCode: 409),
            _ => throw new InvalidOperationException($"Unhandled ranking outcome: {result.Outcome}"),
        };
}
