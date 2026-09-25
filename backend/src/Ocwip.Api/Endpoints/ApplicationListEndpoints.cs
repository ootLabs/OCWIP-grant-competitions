using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Services.Export;
using Ocwip.Api.Services.Pdf;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The operator's view of the applications a competition received (T-35),
/// over HTTP: the list, one submitted offer, and the list exported.
///
/// Every route carries the operator policy, so an applicant or a reviewer is
/// refused before anything is read. The list shows other people's offers by
/// design, which the resource policy on /applications/{id} could never
/// express for a list: it answers for one row at a time.
/// </summary>
public static class ApplicationListEndpoints
{
    internal const string Unavailable =
        "Obsługa wniosków jest chwilowo niedostępna.";

    internal const string CompetitionNotFound = "Nie ma takiego konkursu.";

    internal const string ApplicationNotFound =
        "W tym konkursie nie ma takiego złożonego wniosku.";

    private const string CsvContentType = "text/csv; charset=utf-8";

    public static void MapApplicationListEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapGet("/competitions/{competitionId:guid}/applications",
            async Task<Results<Ok<ApplicationListResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IApplicationListService? lists,
            CancellationToken cancellationToken) =>
        {
            if (lists is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var list = await lists.ListAsync(competitionId, cancellationToken);

            return list is null
                ? TypedResults.Problem(CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(list);
        })
            .WithName("ListCompetitionApplications")
            .WithSummary(
                "The submitted applications of a competition with the requested "
                + "total and what is left of the pool. Drafts are never listed.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/applications/{id:guid}",
            async Task<Results<Ok<SubmittedApplicationResponse>, ProblemHttpResult>> (
            Guid competitionId,
            Guid id,
            [FromServices] IApplicationListService? lists,
            CancellationToken cancellationToken) =>
        {
            if (lists is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var application = await lists.GetSubmittedAsync(
                competitionId, id, cancellationToken);

            return application is null
                ? TypedResults.Problem(ApplicationNotFound, statusCode: 404)
                : TypedResults.Ok(application);
        })
            .WithName("GetSubmittedApplication")
            .WithSummary(
                "One submitted offer with the form version it was filled in on "
                + "and its active attachments. A draft answers 404.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/applications/export/csv",
            (Guid competitionId, [FromServices] IApplicationListService? lists, CancellationToken cancellationToken) =>
                ExportAsync(competitionId, lists, "csv", CsvContentType, ApplicationListCsv.Build, cancellationToken))
            .WithName("ExportCompetitionApplicationsCsv")
            .WithSummary("The same list as a CSV file for a spreadsheet.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/applications/export/pdf",
            (Guid competitionId, [FromServices] IApplicationListService? lists, CancellationToken cancellationToken) =>
                ExportAsync(competitionId, lists, "pdf", "application/pdf", ApplicationListPdfBuilder.Build, cancellationToken))
            .WithName("ExportCompetitionApplicationsPdf")
            .WithSummary("The same list as a PDF to print.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportAsync(
        Guid competitionId,
        IApplicationListService? lists,
        string extension,
        string contentType,
        Func<ApplicationListResponse, byte[]> build,
        CancellationToken cancellationToken)
    {
        if (lists is null)
        {
            return TypedResults.Problem(Unavailable, statusCode: 503);
        }

        var list = await lists.ListAsync(competitionId, cancellationToken);

        if (list is null)
        {
            return TypedResults.Problem(CompetitionNotFound, statusCode: 404);
        }

        // The competition number carries a slash ("1/2026"), which no file
        // system takes in a name.
        var fileName = $"wnioski-{list.CompetitionNumber.Replace('/', '-')}.{extension}";

        return TypedResults.File(build(list), contentType, fileName);
    }
}
