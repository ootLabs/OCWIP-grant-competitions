using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Resignation and the reserve list (T-109), for the operator only: the state
/// after the results (unsigned contracts, the pool, the next reserve), the
/// resignation of a funded application, and the promotion of a reserve one.
/// </summary>
public static class ResignationEndpoints
{
    internal const string NotResolved = "Rezygnacja i lista rezerwowa działają dopiero po zatwierdzeniu wyników konkursu.";
    internal const string NotFunded = "Rezygnację potwierdza się tylko dla wniosku dofinansowanego, bez podpisanej umowy.";
    internal const string NotReserve = "Dofinansowanie z listy rezerwowej przyznaje się tylko wnioskowi z listy rezerwowej.";

    public static void MapResignationEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{competitionId:guid}/resignations",
            async Task<Results<Ok<ResignationsResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IResignationService? resignations,
            CancellationToken cancellationToken) =>
        {
            if (resignations is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await resignations.OverviewAsync(competitionId, cancellationToken);
            return result.Outcome is ResignationOutcome.Succeeded
                ? TypedResults.Ok(result.Overview!)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("GetResignations")
            .WithSummary("Unsigned contracts after the results, the contract deadline, the free pool and the next reserve application.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/applications/{applicationId:guid}/resignation",
            async Task<Results<Ok<ResignationActionResponse>, ProblemHttpResult>> (
            Guid applicationId,
            HttpContext context,
            [FromServices] IResignationService? resignations,
            CancellationToken cancellationToken) =>
        {
            if (resignations is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await resignations.ResignAsync(applicationId, Caller(context), cancellationToken);
            return result.Outcome switch
            {
                ResignationOutcome.Succeeded => TypedResults.Ok(result.Action!),
                ResignationOutcome.NotResolved => TypedResults.Problem(NotResolved, statusCode: 409),
                ResignationOutcome.WrongStatus => TypedResults.Problem(NotFunded, statusCode: 409),
                _ => TypedResults.Problem(GrantDecisionEndpoints.NotFound, statusCode: 404),
            };
        })
            .WithName("ConfirmResignation")
            .WithSummary("Confirms the resignation of a funded application without a signed contract; the applicant gets a mail.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/applications/{applicationId:guid}/promotion",
            async Task<Results<Ok<ResignationActionResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid applicationId,
            PromotionRequest request,
            HttpContext context,
            [FromServices] IResignationService? resignations,
            CancellationToken cancellationToken) =>
        {
            if (resignations is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await resignations.PromoteAsync(applicationId, Caller(context), request, cancellationToken);
            return result.Outcome switch
            {
                ResignationOutcome.Succeeded => TypedResults.Ok(result.Action!),
                ResignationOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                ResignationOutcome.NotResolved => TypedResults.Problem(NotResolved, statusCode: 409),
                ResignationOutcome.WrongStatus => TypedResults.Problem(NotReserve, statusCode: 409),
                _ => TypedResults.Problem(GrantDecisionEndpoints.NotFound, statusCode: 404),
            };
        })
            .WithName("PromoteFromReserve")
            .WithSummary("Funds a reserve application with the given amount, within what is left of the pool; the applicant gets a mail.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);
    }

    private static Guid Caller(HttpContext context) =>
        Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
