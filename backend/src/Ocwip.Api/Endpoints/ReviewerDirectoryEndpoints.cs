using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The two reads the operator's evaluation screen needs to assign experts
/// (T-41): who can evaluate, and who is assigned where. Operator only; an
/// expert never sees the other experts.
/// </summary>
public static class ReviewerDirectoryEndpoints
{
    public static void MapReviewerDirectoryEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/reviewers",
            async Task<Results<Ok<IReadOnlyList<ReviewerSummary>>, ProblemHttpResult>> (
            [FromServices] IReviewerDirectory? directory,
            CancellationToken cancellationToken) =>
        {
            if (directory is null)
            {
                return TypedResults.Problem(RankingEndpoints.Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(await directory.ReviewersAsync(cancellationToken));
        })
            .WithName("ListReviewers")
            .WithSummary("Active expert accounts, the ones an application can be assigned to.")
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/assignments",
            async Task<Results<Ok<IReadOnlyList<CompetitionAssignment>>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IReviewerDirectory? directory,
            CancellationToken cancellationToken) =>
        {
            if (directory is null)
            {
                return TypedResults.Problem(RankingEndpoints.Unavailable, statusCode: 503);
            }

            var rows = await directory.AssignmentsAsync(competitionId, cancellationToken);
            return rows is null
                ? TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(rows);
        })
            .WithName("ListCompetitionAssignments")
            .WithSummary("Every active assignment of a competition: which expert evaluates which application.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);
    }
}
