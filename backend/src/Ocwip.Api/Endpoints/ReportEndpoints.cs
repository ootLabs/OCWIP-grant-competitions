using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Authorization;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Services.Reports;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Reports over HTTP (T-50a). The applicant starts, fills in and submits the
/// report of their own funded application; the operator lists the reports of
/// a competition and accepts or sends one back. Reading a report goes through
/// the resource policy, which grants no expert anything but applications.
/// </summary>
public static class ReportEndpoints
{
    internal const string Unavailable = "Sprawozdania są chwilowo niedostępne.";
    internal const string NotFound = "Nie ma takiego sprawozdania.";
    internal const string NotYours = "Nie masz dostępu do tego sprawozdania.";
    internal const string NotFunded = "Sprawozdanie składa się tylko z dofinansowanego projektu.";
    internal const string NoForm = "Konkurs nie ma jeszcze opublikowanego wzoru sprawozdania.";
    internal const string Frozen = "Sprawozdanie jest złożone. Zmienić je można dopiero, gdy operator zwróci je do poprawy.";
    internal const string WrongState = "Przyjąć albo zwrócić można tylko złożone sprawozdanie.";

    public static void MapReportEndpoints(this WebApplication app)
    {
        var applicantPolicy = AuthorizationConfiguration.Names.For(Role.Applicant);
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/applications/{applicationId:guid}/report",
            async Task<Results<Created<ReportResponse>, Ok<ReportResponse>, ProblemHttpResult>> (
            Guid applicationId,
            HttpContext context,
            [FromServices] IReportService? reports,
            [FromServices] IApplicationService? applications,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (reports is null || applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var resource = await applications.FindForAuthorizationAsync(applicationId, cancellationToken);
            if (resource is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404);
            }

            if (!(await authorization.AuthorizeAsync(context.User, resource, AuthorizationConfiguration.Names.OwnsResource)).Succeeded)
            {
                return TypedResults.Problem(CardSharingEndpoints.NotYours, statusCode: 403);
            }

            var result = await reports.StartAsync(applicationId, cancellationToken);
            return result.Outcome switch
            {
                ReportOutcome.Created => TypedResults.Created($"/reports/{result.Report!.Id}", result.Report),
                ReportOutcome.Succeeded => TypedResults.Ok(result.Report!),
                ReportOutcome.NotFunded => TypedResults.Problem(NotFunded, statusCode: 409),
                ReportOutcome.NoForm => TypedResults.Problem(NoForm, statusCode: 409),
                _ => TypedResults.Problem(EvaluationEndpoints.ApplicationNotFound, statusCode: 404),
            };
        })
            .WithName("StartReport")
            .WithSummary("Starts the report of the caller's own funded application, or hands back the one started.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/reports/{reportId:guid}",
            async Task<Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid reportId,
            HttpContext context,
            [FromServices] IReportService? reports,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await DenyAsync(reports, authorization, context, reportId, cancellationToken) is { } problem)
            {
                return problem;
            }

            return Answer(await reports.GetAsync(reportId, cancellationToken));
        })
            .WithName("GetReport")
            .WithSummary("One report with its form; the operator reads every report, an applicant only their own.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        app.MapPut("/reports/{reportId:guid}",
            async Task<Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid reportId,
            SaveReportRequest request,
            HttpContext context,
            [FromServices] IReportService? reports,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await DenyAsync(reports, authorization, context, reportId, cancellationToken) is { } problem)
            {
                return problem;
            }

            var result = await reports.SaveAsync(reportId, request.Answers, cancellationToken);
            return Answer(result);
        })
            .WithName("SaveReport")
            .WithSummary("Autosave of a report draft; values taken from the application are put back.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(applicantPolicy);

        app.MapPost("/reports/{reportId:guid}/submit",
            async Task<Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid reportId,
            HttpContext context,
            [FromServices] IReportService? reports,
            [FromServices] IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            if (await DenyAsync(reports, authorization, context, reportId, cancellationToken) is { } problem)
            {
                return problem;
            }

            var result = await reports.SubmitAsync(reportId, CallerId(context), cancellationToken);
            return Answer(result);
        })
            .WithName("SubmitReport")
            .WithSummary("Submits the report after checking every required field.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(applicantPolicy);

        app.MapGet("/competitions/{competitionId:guid}/reports",
            async Task<Results<Ok<IReadOnlyList<ReportListItem>>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IReportService? reports,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return await reports.ListAsync(competitionId, cancellationToken) is { } list
                ? TypedResults.Ok(list)
                : TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
        })
            .WithName("ListCompetitionReports")
            .WithSummary("Every report of a competition with its state.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/reports/{reportId:guid}/return",
            async Task<Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid reportId,
            ReturnReportRequest request,
            HttpContext context,
            [FromServices] IReportService? reports,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var result = await reports.ReturnAsync(reportId, CallerId(context), request.Reason, cancellationToken);
            return Answer(result);
        })
            .WithName("ReturnReport")
            .WithSummary("Sends a submitted report back to the applicant with a reason.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/reports/{reportId:guid}/accept",
            async Task<Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult>> (
            Guid reportId,
            HttpContext context,
            [FromServices] IReportService? reports,
            CancellationToken cancellationToken) =>
        {
            if (reports is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            return Answer(await reports.AcceptAsync(reportId, CallerId(context), cancellationToken));
        })
            .WithName("AcceptReport")
            .WithSummary("Accepts a submitted report.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(operatorPolicy);
    }

    private static async Task<ProblemHttpResult?> DenyAsync(
        IReportService reports,
        IAuthorizationService authorization,
        HttpContext context,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var resource = await reports.FindForAuthorizationAsync(reportId, cancellationToken);
        if (resource is null)
        {
            return TypedResults.Problem(NotFound, statusCode: 404);
        }

        var allowed = await authorization.AuthorizeAsync(context.User, resource, AuthorizationConfiguration.Names.OwnsResource);
        return allowed.Succeeded ? null : TypedResults.Problem(NotYours, statusCode: 403);
    }

    private static Results<Ok<ReportResponse>, ValidationProblem, ProblemHttpResult> Answer(ReportResult result) =>
        result.Outcome switch
        {
            ReportOutcome.Succeeded => TypedResults.Ok(result.Report!),
            ReportOutcome.Invalid => TypedResults.ValidationProblem(result.Errors!),
            ReportOutcome.Frozen => TypedResults.Problem(Frozen, statusCode: 409),
            ReportOutcome.WrongState => TypedResults.Problem(WrongState, statusCode: 409),
            _ => TypedResults.Problem(NotFound, statusCode: 404),
        };

    private static Guid CallerId(HttpContext context) =>
        Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
