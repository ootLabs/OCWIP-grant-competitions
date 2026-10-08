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
/// "Zwrot do poprawy" (T-103): the operator returns a submitted application;
/// whoever may read the application reads its returns, earlier versions and
/// status history. Correcting and submitting again go through the existing
/// draft and submit routes, which ApplicationEditWindow opens for a returned
/// application.
/// </summary>
public static class ApplicationReturnEndpoints
{
    internal const string NotReturnable =
        "Zwrócić do poprawy można tylko złożony wniosek, który nie jest już zwrócony i nie ma jeszcze wyniku.";

    internal const string WrongStage =
        "Zwrot do poprawy jest możliwy w trakcie naboru i oceny, przed zatwierdzeniem wyników.";

    internal const string NoVersion = "Wniosek nie ma takiej wcześniejszej wersji.";

    internal const string NotForReviewer =
        "Uwagi zwrotu i wcześniejsze wersje wniosku są dostępne dla operatora i wnioskodawcy.";

    /// <summary>
    /// The return is between the operator and the applicant (S-22): one writes
    /// what to correct, the other reads it. An assigned expert reads the
    /// application and its attachments (T-40), not the conversation about
    /// correcting it, and an earlier version is the other half of that
    /// conversation. The resource check passes for them, because the
    /// assignment makes the application theirs to read, so the refusal lives
    /// here, on the two routes that carry the notes.
    ///
    /// Since R-44 an applicant appointed to another competition's committee
    /// carries the expert's claim too, so the question is not the claim but
    /// the card: whoever acts for the applicant (T-93a) reads the notes, the
    /// operator reads everything, and an expert who does not act for the
    /// applicant does not. That is the member policy, asked after the
    /// resource policy so a stranger still gets the plain refusal.
    /// </summary>
    private static async Task<ProblemHttpResult?> RefuseReviewerAsync(
        IApplicationService applications,
        IAuthorizationService authorization,
        HttpContext context,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var resource = await applications.FindForAuthorizationAsync(applicationId, cancellationToken);

        var member = resource is not null
            && (await authorization.AuthorizeAsync(
                context.User, resource, AuthorizationConfiguration.Names.MemberOfResource)).Succeeded;

        return member ? null : TypedResults.Problem(NotForReviewer, statusCode: StatusCodes.Status403Forbidden);
    }

    public static void MapApplicationReturnEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/applications/{id:guid}/return",
            async Task<Results<Created<ApplicationReturnResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid id,
            ApplicationReturnRequest request,
            HttpContext context,
            [FromServices] IApplicationReturnService? returns,
            CancellationToken cancellationToken) =>
        {
            if (returns is null)
            {
                return TypedResults.Problem(ApplicationEndpoints.Unavailable, statusCode: 503);
            }

            var operatorId = Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await returns.ReturnAsync(id, operatorId, request, cancellationToken);

            return result.Outcome switch
            {
                ApplicationReturnOutcome.Created => TypedResults.Created($"/applications/{id}/corrections", result.Return!),
                ApplicationReturnOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
                ApplicationReturnOutcome.NotReturnable => TypedResults.Problem(NotReturnable, statusCode: 409),
                ApplicationReturnOutcome.WrongStage => TypedResults.Problem(WrongStage, statusCode: 409),
                _ => TypedResults.Problem(ApplicationEndpoints.NotFound, statusCode: 404),
            };
        })
            .WithName("ReturnApplication")
            .WithSummary("Sends a submitted application back for correction: sections, note, deadline. The applicant gets a mail.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/applications/{id:guid}/corrections",
            async Task<Results<Ok<ApplicationCorrectionsResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IApplicationService? applications,
            [FromServices] IApplicationReturnService? returns,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null || returns is null)
            {
                return TypedResults.Problem(ApplicationEndpoints.Unavailable, statusCode: 503);
            }

            if (await ApplicationEndpoints.AuthorizeAsync(applications, authorization, context, id, cancellationToken) is { } problem)
            {
                return problem;
            }

            if (await RefuseReviewerAsync(applications, authorization, context, id, cancellationToken) is { } notTheirs)
            {
                return notTheirs;
            }

            var result = await returns.CorrectionsAsync(id, cancellationToken);
            return result.Outcome is ApplicationReturnOutcome.Succeeded
                ? TypedResults.Ok(result.Corrections!)
                : TypedResults.Problem(ApplicationEndpoints.NotFound, statusCode: 404);
        })
            .WithName("GetApplicationCorrections")
            .WithSummary("The returns of an application, its earlier submitted versions and its status history.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapGet("/applications/{id:guid}/versions/{version:int}",
            async Task<Results<Ok<ApplicationVersionResponse>, ProblemHttpResult>> (
            Guid id,
            int version,
            [FromServices] IApplicationService? applications,
            [FromServices] IApplicationReturnService? returns,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null || returns is null)
            {
                return TypedResults.Problem(ApplicationEndpoints.Unavailable, statusCode: 503);
            }

            if (await ApplicationEndpoints.AuthorizeAsync(applications, authorization, context, id, cancellationToken) is { } problem)
            {
                return problem;
            }

            if (await RefuseReviewerAsync(applications, authorization, context, id, cancellationToken) is { } notTheirs)
            {
                return notTheirs;
            }

            var result = await returns.VersionAsync(id, version, cancellationToken);
            return result.Outcome is ApplicationReturnOutcome.Succeeded
                ? TypedResults.Ok(result.Version!)
                : TypedResults.Problem(NoVersion, statusCode: 404);
        })
            .WithName("GetApplicationVersion")
            // T-47a: an earlier version holds the same personal data as the application.
            .LogsPersonalDataRead("application", "id")
            .WithSummary("An earlier submitted version of an application, as it was submitted.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }
}
