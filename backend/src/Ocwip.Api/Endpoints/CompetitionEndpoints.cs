using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The competition, over HTTP (T-20).
///
/// Two groups of routes and the difference between them is the whole point of
/// the file. Everything under /competitions belongs to the operator and says
/// so with the role policy from T-13.2. Everything under /public/competitions
/// is readable with no account, and says THAT out loud with AllowAnonymous,
/// because the fallback policy refuses anything that declares nothing.
///
/// The intake cut off (T-21), the operator screens (T-22) and the public pages
/// (T-23) build on these and are not here.
/// </summary>
public static class CompetitionEndpoints
{
    internal const string Unavailable =
        "Obsługa konkursów jest chwilowo niedostępna.";

    internal const string NotFound = "Nie ma takiego konkursu.";

    internal const string NumberTaken =
        "Inny konkurs ma już ten numer. Numer konkursu musi być niepowtarzalny.";

    internal const string UnknownFormDefinition =
        "Wskazana wersja formularza nie należy do tego konkursu.";

    internal const string UnknownContact =
        "Osoba kontaktowa musi być aktywnym kontem pracownika OCWIP.";

    internal const string Inactive =
        "Ten konkurs jest oznaczony jako nieaktywny, więc nie można go zmieniać.";

    /// <summary>
    /// D12: the message names the value that decided it, so it says where the
    /// competition actually is rather than repeating that the move is refused.
    /// </summary>
    internal static string TransitionNotAllowed(
        CompetitionStatus current,
        CompetitionStatus target) =>
        $"Nie można przestawić konkursu ze stanu \"{Name(current)}\" "
        + $"na \"{Name(target)}\".";

    /// <summary>
    /// The state names as docs/slownik.md and the report spell them, because
    /// this text is read by a person. The enum stays English, see AGENTS.md.
    /// </summary>
    internal static string Name(CompetitionStatus status) => status switch
    {
        CompetitionStatus.Draft => "roboczy",
        CompetitionStatus.Published => "opublikowany",
        CompetitionStatus.OpenForApplications => "trwa nabór",
        CompetitionStatus.Closed => "nabór zamknięty",
        CompetitionStatus.UnderReview => "trwa ocena",
        CompetitionStatus.Resolved => "rozstrzygnięty",
        CompetitionStatus.Archived => "archiwalny",

        // Not a default that invents a name: a state added to the enum without
        // a Polish one shows up as the identifier rather than as silence, and
        // that is visible enough to be fixed.
        _ => status.ToString(),
    };

    public static void MapCompetitionEndpoints(this WebApplication app)
    {
        MapOperatorEndpoints(app);
        MapPublicEndpoints(app);
    }

    private static void MapOperatorEndpoints(WebApplication app)
    {
        // Every route below carries the operator policy. Applied per route and
        // not to a group, because a group is a thing somebody can add a route
        // outside of, and the failure would look exactly like a route that
        // belongs there.
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/competitions", async Task<Results<
            Created<CompetitionResponse>, ValidationProblem, ProblemHttpResult>> (
            CompetitionRequest request,
            // Explicit and nullable for the reason written out in
            // AccountEndpoints: the service exists only when a connection
            // string does, and letting the binder look the type up at
            // endpoint-build time takes down routing for the whole app on a
            // host without one.
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            request = CompetitionRequestValidator.Trim(request);

            var problems = CompetitionRequestValidator.Validate(request);
            if (problems.Count > 0)
            {
                return TypedResults.ValidationProblem(problems);
            }

            var result = await competitions.CreateAsync(request, cancellationToken);

            return result.Outcome is CompetitionOutcome.Succeeded
                ? TypedResults.Created(
                    $"/competitions/{result.Competition!.Id}", result.Competition)
                : Failure(result, CompetitionStatus.Draft);
        })
            .WithName("CreateCompetition")
            .WithSummary(
                "Creates a competition. It always starts as a draft: publishing "
                + "is a separate, deliberate step.")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions", async Task<Results<
            Ok<IReadOnlyList<CompetitionResponse>>, ProblemHttpResult>> (
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(
                await competitions.ListAsync(cancellationToken));
        })
            .WithName("ListCompetitions")
            .WithSummary(
                "Every competition, drafts and inactive ones included.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{id:guid}", async Task<Results<
            Ok<CompetitionResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await competitions.GetAsync(id, cancellationToken);

            return result.Outcome is CompetitionOutcome.Succeeded
                ? TypedResults.Ok(result.Competition!)
                : Failure(result, CompetitionStatus.Draft);
        })
            .WithName("GetCompetition")
            .WithSummary("One competition, as the operator sees it.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapPut("/competitions/{id:guid}", async Task<Results<
            Ok<CompetitionResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid id,
            CompetitionRequest request,
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            request = CompetitionRequestValidator.Trim(request);

            var problems = CompetitionRequestValidator.Validate(request);
            if (problems.Count > 0)
            {
                return TypedResults.ValidationProblem(problems);
            }

            var result = await competitions.UpdateAsync(
                id, request, cancellationToken);

            return result.Outcome is CompetitionOutcome.Succeeded
                ? TypedResults.Ok(result.Competition!)
                : Failure(result, CompetitionStatus.Draft);
        })
            .WithName("UpdateCompetition")
            .WithSummary("Changes the settings of a competition.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competitions/{id:guid}/status", async Task<Results<
            Ok<CompetitionResponse>, ProblemHttpResult>> (
            Guid id,
            CompetitionStatusChangeRequest request,
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await competitions.ChangeStatusAsync(
                id, request.Status, cancellationToken);

            return result.Outcome is CompetitionOutcome.Succeeded
                ? TypedResults.Ok(result.Competition!)
                : Failure(result, request.Status);
        })
            .WithName("ChangeCompetitionStatus")
            .WithSummary(
                "Moves the competition one step through its lifecycle, when the "
                + "transition table allows an operator to make that step.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapDelete("/competitions/{id:guid}", async Task<Results<
            Ok<CompetitionResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await competitions.DeactivateAsync(id, cancellationToken);

            return result.Outcome is CompetitionOutcome.Succeeded
                ? TypedResults.Ok(result.Competition!)
                : Failure(result, CompetitionStatus.Draft);
        })
            .WithName("DeactivateCompetition")
            .WithSummary(
                "Marks the competition inactive. Nothing is deleted: the row "
                + "stays for the retention period and only leaves the public "
                + "listing.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }

    private static void MapPublicEndpoints(WebApplication app)
    {
        app.MapGet("/public/competitions", async Task<Results<
            Ok<IReadOnlyList<PublicCompetitionResponse>>, ProblemHttpResult>> (
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return TypedResults.Ok(
                await competitions.ListPublicAsync(cancellationToken));
        })
            .WithName("ListPublicCompetitions")
            .WithSummary(
                "Announced competitions, readable without an account. Drafts "
                + "and archived competitions are not here.")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            // T-13.2, and D6 is the reason it is anonymous at all: an applicant
            // should not have to create an account to find out what OCWIP is
            // running.
            .AllowAnonymous();

        app.MapGet("/public/competitions/{id:guid}", async Task<Results<
            Ok<PublicCompetitionResponse>, ProblemHttpResult>> (
            Guid id,
            [FromServices] ICompetitionService? competitions,
            CancellationToken cancellationToken) =>
        {
            if (competitions is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var competition = await competitions.GetPublicAsync(id, cancellationToken);

            // 404 and not 403 for a draft, and this is the line the card is
            // about: a draft has no public address. Answering 403 would tell
            // whoever guessed the identifier that they guessed right.
            return competition is null
                ? TypedResults.Problem(NotFound, statusCode: 404)
                : TypedResults.Ok(competition);
        })
            .WithName("GetPublicCompetition")
            .WithSummary(
                "One announced competition, readable without an account. A "
                + "draft answers 404, because it has no public address.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .AllowAnonymous();
    }

    /// <summary>
    /// One outcome, one status code, in one place. Spelling this out at each
    /// route would be six copies of the same mapping, and the copies are what
    /// drift once an outcome is added.
    /// </summary>
    private static ProblemHttpResult Failure(
        CompetitionResult result,
        CompetitionStatus target) => result.Outcome switch
        {
            CompetitionOutcome.NotFound =>
                TypedResults.Problem(NotFound, statusCode: 404),

            // 409 rather than 400 for all three: the body is well formed and
            // there is nothing in it to correct. What is in the way is the
            // state of something else.
            CompetitionOutcome.NumberTaken =>
                TypedResults.Problem(NumberTaken, statusCode: 409),

            CompetitionOutcome.UnknownFormDefinition =>
                TypedResults.Problem(UnknownFormDefinition, statusCode: 409),

            CompetitionOutcome.Inactive =>
                TypedResults.Problem(Inactive, statusCode: 409),

            CompetitionOutcome.UnknownContact =>
                TypedResults.Problem(UnknownContact, statusCode: 409),

            CompetitionOutcome.TransitionNotAllowed =>
                TypedResults.Problem(
                    TransitionNotAllowed(result.CurrentStatus!.Value, target),
                    statusCode: 409),

            // Succeeded never reaches here, and a new outcome should arrive as
            // a visible 500 rather than as a silently successful answer.
            _ => throw new InvalidOperationException(
                $"Unhandled competition outcome: {result.Outcome}"),
        };
}
