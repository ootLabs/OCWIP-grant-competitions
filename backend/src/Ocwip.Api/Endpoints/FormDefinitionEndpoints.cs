using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Versions of the form of a competition, over HTTP (T-25).
///
/// Three routes and the missing fourth is the design: there is no route that
/// replaces a stored document. Publishing the next version is the only way to
/// change a form, because applications point at a version and the one they
/// point at has to stay exactly as it was shown.
///
/// The publishing screen (T-27) and the creator (T-26) sit on top of these.
/// </summary>
public static class FormDefinitionEndpoints
{
    internal const string Unavailable =
        "Obsługa formularzy jest chwilowo niedostępna.";

    internal const string CompetitionNotFound = "Nie ma takiego konkursu.";

    internal const string NotFound =
        "Nie ma takiej wersji formularza.";

    internal const string Inactive =
        "Ten konkurs jest oznaczony jako nieaktywny, więc nie można "
        + "publikować jego formularza.";

    internal const string VersionTaken =
        "Ktoś opublikował nowszą wersję formularza w tej samej chwili. "
        + "Spróbuj jeszcze raz.";

    public static void MapFormDefinitionEndpoints(this WebApplication app)
    {
        // Applied per route rather than to a group, for the reason spelled out
        // in CompetitionEndpoints: a group is a thing somebody can add a route
        // outside of, and that mistake looks exactly like a route that belongs.
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/competitions/{competitionId:guid}/form-definitions",
            async Task<Results<
                Created<FormDefinitionResponse>,
                ValidationProblem,
                ProblemHttpResult>> (
            Guid competitionId,
            FormDefinitionRequest request,
            // Explicit and nullable, see AccountEndpoints: the service exists
            // only when a connection string does, and letting the binder
            // resolve the type while endpoints are built takes routing down
            // for the whole app on a host without one.
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await definitions.PublishAsync(
                competitionId, request, cancellationToken);

            // The one outcome that is about the body, so the one that does
            // not go through the shared mapping below: it answers 400 with the
            // fields named rather than a single sentence.
            if (result.Outcome is FormDefinitionOutcome.InvalidDefinition)
            {
                return TypedResults.ValidationProblem(Problems(result.Errors!));
            }

            if (result.Outcome is not FormDefinitionOutcome.Succeeded)
            {
                return Failure(result);
            }

            var published = result.Definition!;

            return TypedResults.Created(
                $"/competitions/{competitionId}/form-definitions"
                    + $"/{published.VersionNumber}",
                published);
        })
            .WithName("PublishFormDefinition")
            .WithSummary(
                "Publishes the next version of the form of a competition. It "
                + "adds a version and never replaces one: applications already "
                + "started keep the version they were filled against.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/form-definitions",
            async Task<Results<
                Ok<IReadOnlyList<FormDefinitionSummaryResponse>>,
                ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var versions = await definitions.ListAsync(
                competitionId, cancellationToken);

            return versions is null
                ? TypedResults.Problem(CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(versions);
        })
            .WithName("ListFormDefinitions")
            .WithSummary(
                "Every published version of the form of a competition, oldest "
                + "first, without the documents.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet(
            "/competitions/{competitionId:guid}/form-definitions/{version:int}",
            async Task<Results<Ok<FormDefinitionResponse>, ProblemHttpResult>> (
            Guid competitionId,
            int version,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await definitions.GetAsync(
                competitionId, version, cancellationToken);

            return result.Outcome is FormDefinitionOutcome.Succeeded
                ? TypedResults.Ok(result.Definition!)
                : Failure(result);
        })
            .WithName("GetFormDefinition")
            .WithSummary(
                "One version of the form with its document, addressed by the "
                + "version number an application names.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }

    /// <summary>
    /// One outcome, one status code, in one place. Written out at each route
    /// it would be three copies of the same mapping, and copies are what drift
    /// once an outcome is added.
    /// </summary>
    private static ProblemHttpResult Failure(FormDefinitionResult result) =>
        result.Outcome switch
        {
            // Both 404, and the message is the difference: it names the thing
            // that is actually missing, so an operator who mistyped the
            // competition is not sent looking for a version.
            FormDefinitionOutcome.CompetitionNotFound =>
                TypedResults.Problem(CompetitionNotFound, statusCode: 404),

            FormDefinitionOutcome.NotFound =>
                TypedResults.Problem(NotFound, statusCode: 404),

            // 409 and not 400: the body is well formed and there is nothing in
            // it to correct. What is in the way is the state of something else.
            FormDefinitionOutcome.Inactive =>
                TypedResults.Problem(Inactive, statusCode: 409),

            FormDefinitionOutcome.VersionTaken =>
                TypedResults.Problem(VersionTaken, statusCode: 409),

            // InvalidDefinition never reaches here: it is answered at the one
            // route that can produce it, with the fields named. Succeeded
            // never reaches here either, and an outcome added later should
            // arrive as a visible 500 rather than as a quiet success.
            _ => throw new InvalidOperationException(
                $"Unhandled form definition outcome: {result.Outcome}"),
        };

    /// <summary>
    /// The refusals of the contract gate as a validation problem: the JSON
    /// path of the place in the document is the key, so two problems with the
    /// same field arrive together instead of one hiding the other.
    /// </summary>
    internal static IDictionary<string, string[]> Problems(
        IReadOnlyList<FormSchemaError> errors) =>
        errors
            .GroupBy(error => error.Path)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).ToArray());
}
