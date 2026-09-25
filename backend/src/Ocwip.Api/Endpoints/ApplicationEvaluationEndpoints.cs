using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The cards of one application for the operator (T-41a): the formal card and
/// every expert's merit card with the expert's name. Operator only: an expert
/// never reads another expert's card (independence of the evaluations, T-38).
/// </summary>
public static class ApplicationEvaluationEndpoints
{
    public static void MapApplicationEvaluationEndpoints(this WebApplication app)
    {
        app.MapGet("/applications/{applicationId:guid}/evaluations",
            async Task<Results<Ok<IReadOnlyList<ApplicationEvaluationItem>>, ProblemHttpResult>> (
            Guid applicationId,
            [FromServices] IApplicationEvaluationList? list,
            CancellationToken cancellationToken) =>
        {
            if (list is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var items = await list.ListAsync(applicationId, cancellationToken);
            return items is null
                ? TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404)
                : TypedResults.Ok(items);
        })
            .WithName("ListApplicationEvaluations")
            .WithSummary("Every card of an application, formal first, with who filled each in.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Operator));
    }
}
