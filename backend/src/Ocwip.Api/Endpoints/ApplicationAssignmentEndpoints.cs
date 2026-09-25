using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Assigning and revoking reviewers on an application, over HTTP (T-37).
///
/// Both routes carry the Operator role policy directly, not the resource
/// policy from T-13.2: an operator manages assignments on every application,
/// there is no owner to check against. The rule that DOES depend on the
/// resource is on the other side of this feature, in
/// Authorization/EntityScopedHandler.cs, which decides whether the assigned
/// reviewer may then read the application.
/// </summary>
public static class ApplicationAssignmentEndpoints
{
    internal const string Unavailable =
        "Obsługa przypisań recenzentów jest chwilowo niedostępna.";

    internal const string ApplicationNotFound = "Nie ma takiego wniosku.";

    internal const string ReviewerNotFound =
        "Nie ma aktywnego konta recenzenta o tym identyfikatorze.";

    internal const string NotAssigned =
        "Ten recenzent nie jest przypisany do tego wniosku.";

    public static void MapApplicationAssignmentEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/applications/{id:guid}/assignments",
            async Task<Results<Ok<ApplicationAssignmentResponse>, ProblemHttpResult>> (
            Guid id,
            AssignReviewerRequest request,
            [FromServices] IApplicationAssignmentService? assignments,
            CancellationToken cancellationToken) =>
        {
            if (assignments is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await assignments.AssignAsync(
                id, request.ReviewerId, cancellationToken);

            return result.Outcome is ApplicationAssignmentOutcome.Succeeded
                ? TypedResults.Ok(result.Assignment!)
                : Failure(result);
        })
            .WithName("AssignReviewer")
            .WithSummary(
                "Assigns a reviewer to an application. Many to many: the "
                + "same application may take several reviewers and the same "
                + "reviewer several applications. Idempotent.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapDelete("/applications/{id:guid}/assignments/{reviewerId:guid}",
            async Task<Results<Ok<ApplicationAssignmentResponse>, ProblemHttpResult>> (
            Guid id,
            Guid reviewerId,
            [FromServices] IApplicationAssignmentService? assignments,
            CancellationToken cancellationToken) =>
        {
            if (assignments is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await assignments.UnassignAsync(
                id, reviewerId, cancellationToken);

            return result.Outcome is ApplicationAssignmentOutcome.Succeeded
                ? TypedResults.Ok(result.Assignment!)
                : Failure(result);
        })
            .WithName("UnassignReviewer")
            .WithSummary(
                "Revokes a reviewer's assignment. Never a hard delete "
                + "(AGENTS.md rule 5): the row stays, marked inactive.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }

    private static ProblemHttpResult Failure(ApplicationAssignmentResult result) =>
        result.Outcome switch
        {
            ApplicationAssignmentOutcome.ApplicationNotFound =>
                TypedResults.Problem(ApplicationNotFound, statusCode: 404),

            ApplicationAssignmentOutcome.ReviewerNotFound =>
                TypedResults.Problem(ReviewerNotFound, statusCode: 404),

            ApplicationAssignmentOutcome.NotAssigned =>
                TypedResults.Problem(NotAssigned, statusCode: 404),

            // Succeeded never reaches here, and a new outcome should arrive
            // as a visible 500 rather than as a silently successful answer.
            _ => throw new InvalidOperationException(
                $"Unhandled application assignment outcome: {result.Outcome}"),
        };
}
