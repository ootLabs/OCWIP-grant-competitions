using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Sharing the evaluation cards with the applicants (T-41b, report step 5.5):
/// the operator decides once for the whole competition, and from then on an
/// applicant reads the finished cards of their own application with nothing
/// about who evaluated it.
/// </summary>
public static class CardSharingEndpoints
{
    internal const string AlreadyShared =
        "Karty oceny tego konkursu są już udostępnione wnioskodawcom. Tej decyzji nie można cofnąć.";

    internal const string NotYours = "Nie masz dostępu do tego wniosku.";

    public static void MapCardSharingEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{competitionId:guid}/card-sharing",
            async Task<Results<Ok<CardSharingResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] ICardSharingService? sharing,
            CancellationToken cancellationToken) =>
        {
            if (sharing is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await sharing.GetAsync(competitionId, cancellationToken);
            return result.Outcome is CardSharingOutcome.Succeeded
                ? TypedResults.Ok(result.Sharing!)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("GetCardSharing")
            .WithSummary("Whether and when the evaluation cards of a competition were shared with the applicants.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{competitionId:guid}/card-sharing",
            async Task<Results<Ok<CardSharingResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] ICardSharingService? sharing,
            CancellationToken cancellationToken) =>
        {
            if (sharing is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            var result = await sharing.ShareAsync(competitionId, cancellationToken);
            return result.Outcome switch
            {
                CardSharingOutcome.Succeeded => TypedResults.Ok(result.Sharing!),
                CardSharingOutcome.AlreadyShared => TypedResults.Problem(AlreadyShared, statusCode: 409),
                _ => TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404),
            };
        })
            .WithName("ShareEvaluationCards")
            .WithSummary("Shares the evaluation cards of a competition with the applicants, once and for good.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/applications/{applicationId:guid}/evaluation-cards",
            async Task<Results<Ok<ApplicantEvaluationCards>, ProblemHttpResult>> (
            Guid applicationId,
            HttpContext context,
            [FromServices] ICardSharingService? sharing,
            [FromServices] IApplicationService? applications,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (sharing is null || applications is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            // The applicant role AND ownership. The resource policy alone
            // would let an assigned expert in too, and the other experts'
            // cards are exactly what an expert must not read (T-38).
            var resource = await applications.FindForAuthorizationAsync(applicationId, cancellationToken);

            if (resource is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404);
            }

            var allowed = await authorization.AuthorizeAsync(
                context.User, resource, AuthorizationConfiguration.Names.OwnsResource);

            if (!allowed.Succeeded)
            {
                return TypedResults.Problem(NotYours, statusCode: 403);
            }

            var cards = await sharing.ForApplicantAsync(applicationId, cancellationToken);
            return cards is null
                ? TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404)
                : TypedResults.Ok(cards);
        })
            .WithName("GetApplicantEvaluationCards")
            .WithSummary("The finished evaluation cards of the caller's own application, once shared, without their authors.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationConfiguration.Names.For(Role.Applicant));
    }
}
