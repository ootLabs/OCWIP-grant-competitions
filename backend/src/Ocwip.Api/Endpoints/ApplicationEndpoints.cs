using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Draft applications, over HTTP (T-29).
///
/// Creation is scoped by role: only an Applicant may start one, and always
/// under their own Podmiot, decided from the session rather than from an id
/// in the body. Reading, saving and deactivating one address it by id and
/// carry no role policy of their own; the resource policy from T-13.2 decides
/// per request, the same order every scoped resource in this product follows:
/// load the row, ask the authorization service, only then act. See
/// Tests/Authorization/PolicyProbeEndpoints.cs, which proved this order before
/// any product endpoint used it.
/// </summary>
public static class ApplicationEndpoints
{
    internal const string Unavailable =
        "Obsługa wniosków jest chwilowo niedostępna.";

    internal const string CompetitionNotFound = "Nie ma takiego konkursu.";

    internal const string NoFormDefinition =
        "Ten konkurs nie ma jeszcze opublikowanego formularza, więc nie można "
        + "rozpocząć wniosku.";

    internal const string NoEntity =
        "Twoje konto nie ma przypisanego podmiotu, więc nie może złożyć wniosku.";

    internal const string NotFound = "Nie ma takiego wniosku.";

    internal const string Forbidden = "Nie masz dostępu do tego wniosku.";

    internal const string AlreadySubmitted =
        "Ten wniosek został już złożony, więc nie można go już zmieniać.";

    internal const string InvalidAnswers =
        "Odpowiedzi muszą być obiektem albo listą.";

    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);

        app.MapPost("/competitions/{competitionId:guid}/applications",
            async Task<Results<Created<ApplicationResponse>, ProblemHttpResult>> (
            Guid competitionId,
            // Explicit and nullable for the reason written out in
            // AccountEndpoints: the service exists only when a connection
            // string does, and letting the binder resolve the type while
            // endpoints are built takes routing down for the whole app on a
            // host without one.
            [FromServices] IApplicationService? applications,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await applications.CreateDraftAsync(
                competitionId, context.User, cancellationToken);

            return result.Outcome is ApplicationOutcome.Succeeded
                ? TypedResults.Created(
                    $"/applications/{result.Application!.Id}", result.Application)
                : Failure(result);
        })
            .WithName("CreateApplicationDraft")
            .WithSummary(
                "Starts an empty draft for the caller's own Podmiot against a "
                + "competition's current form. Filling it in is PUT "
                + "/applications/{id}.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/applications/{id:guid}",
            async Task<Results<Ok<ApplicationResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IApplicationService? applications,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                applications, authorization, context, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var result = await applications.GetAsync(id, cancellationToken);

            return result.Outcome is ApplicationOutcome.Succeeded
                ? TypedResults.Ok(result.Application!)
                : Failure(result);
        })
            .WithName("GetApplication")
            .WithSummary(
                "One application exactly as it was left, draft or submitted.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapPut("/applications/{id:guid}",
            async Task<Results<Ok<ApplicationResponse>, ProblemHttpResult>> (
            Guid id,
            SaveApplicationDraftRequest request,
            [FromServices] IApplicationService? applications,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                applications, authorization, context, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var result = await applications.SaveDraftAsync(
                id, request.Answers, cancellationToken);

            return result.Outcome is ApplicationOutcome.Succeeded
                ? TypedResults.Ok(result.Application!)
                : Failure(result);
        })
            .WithName("SaveApplicationDraft")
            .WithSummary(
                "Autosave: overwrites the stored answers with whatever the "
                + "form holds right now. Refused once the competition's "
                + "intake has closed (T-21) or the application has already "
                + "been submitted.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapDelete("/applications/{id:guid}",
            async Task<Results<Ok<ApplicationResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] IApplicationService? applications,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                applications, authorization, context, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var result = await applications.DeactivateAsync(id, cancellationToken);

            return result.Outcome is ApplicationOutcome.Succeeded
                ? TypedResults.Ok(result.Application!)
                : Failure(result);
        })
            .WithName("DeactivateApplicationDraft")
            .WithSummary(
                "Marks a draft inactive. Never a hard delete (AGENTS.md rule "
                + "5): the row and its answers stay for the retention period.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }

    /// <summary>
    /// Load, then ask, then answer, in one place so the three routes above
    /// spell it out once. A row that is not there is 404; a row that is there
    /// and is not the caller's, per the resource policy from T-13.2, is 403.
    /// Null means neither happened and the caller may proceed.
    /// </summary>
    private static async Task<ProblemHttpResult?> AuthorizeAsync(
        IApplicationService applications,
        IAuthorizationService authorization,
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var resource = await applications.FindForAuthorizationAsync(
            id, cancellationToken);

        if (resource is null)
        {
            return TypedResults.Problem(NotFound, statusCode: 404);
        }

        var authorized = await authorization.AuthorizeAsync(
            context.User, resource, AuthorizationConfiguration.Names.OwnsResource);

        return authorized.Succeeded
            ? null
            : TypedResults.Problem(Forbidden, statusCode: 403);
    }

    /// <summary>
    /// One outcome, one status code, in one place. Spelling this out at each
    /// route would be four copies of the same mapping, and copies are what
    /// drift once an outcome is added.
    /// </summary>
    private static ProblemHttpResult Failure(ApplicationResult result) =>
        result.Outcome switch
        {
            ApplicationOutcome.CompetitionNotFound =>
                TypedResults.Problem(CompetitionNotFound, statusCode: 404),

            ApplicationOutcome.NotFound =>
                TypedResults.Problem(NotFound, statusCode: 404),

            ApplicationOutcome.NoFormDefinition =>
                TypedResults.Problem(NoFormDefinition, statusCode: 409),

            // The message names the moment that decided it (D12), so it is
            // carried on the result rather than one fixed string here.
            ApplicationOutcome.IntakeClosed =>
                TypedResults.Problem(result.Message!, statusCode: 409),

            ApplicationOutcome.NoEntity =>
                TypedResults.Problem(NoEntity, statusCode: 403),

            ApplicationOutcome.AlreadySubmitted =>
                TypedResults.Problem(AlreadySubmitted, statusCode: 409),

            ApplicationOutcome.InvalidAnswers =>
                TypedResults.Problem(InvalidAnswers, statusCode: 400),

            // Succeeded never reaches here, and a new outcome should arrive as
            // a visible 500 rather than as a silently successful answer.
            _ => throw new InvalidOperationException(
                $"Unhandled application outcome: {result.Outcome}"),
        };
}
