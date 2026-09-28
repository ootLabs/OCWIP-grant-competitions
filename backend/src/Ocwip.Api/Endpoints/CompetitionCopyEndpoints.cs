using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// "Skopiuj konkurs" (T-98): a new draft from a previous edition, with a new
/// number and dates, for the operator only. Kept apart from
/// CompetitionEndpoints, which is long enough already.
/// </summary>
public static class CompetitionCopyEndpoints
{
    public static void MapCompetitionCopyEndpoints(this WebApplication app)
    {
        app.MapPost("/competitions/{id:guid}/copy",
            async Task<Results<Created<CompetitionResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid id,
            CompetitionCopyRequest request,
            [FromServices] ICompetitionCopyService? copies,
            CancellationToken cancellationToken) =>
        {
            if (copies is null)
            {
                return TypedResults.Problem(CompetitionEndpoints.Unavailable, statusCode: 503);
            }

            var result = await copies.CopyAsync(id, request, cancellationToken);
            return result.Outcome switch
            {
                CompetitionCopyOutcome.Created => TypedResults.Created($"/competitions/{result.Competition!.Id}", result.Competition),
                CompetitionCopyOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                CompetitionCopyOutcome.NumberTaken => TypedResults.Problem(CompetitionEndpoints.NumberTaken, statusCode: 409),
                _ => TypedResults.Problem(CompetitionEndpoints.NotFound, statusCode: 404),
            };
        })
            .WithName("CopyCompetition")
            .WithSummary("A new draft competition from this one: settings, lists, forms, cards, report form and contract template, with a new number and dates.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Operator));
    }
}
