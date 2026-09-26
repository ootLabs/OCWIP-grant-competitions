using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// The ranking list out of the system (T-42a): three files for the operator
/// and the published results for everybody, once approved.
/// </summary>
public static class RankingPublicationEndpoints
{
    internal const string NoResults = "Ten konkurs nie ma jeszcze ogłoszonych wyników.";

    public static void MapRankingPublicationEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        foreach (var (format, contentType, write) in new (string, string, Func<RankingExport, byte[]>)[]
        {
            ("csv", "text/csv; charset=utf-8", RankingExportWriters.Csv),
            ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", RankingExportWriters.Xlsx),
            ("pdf", "application/pdf", RankingExportWriters.Pdf),
        })
        {
            app.MapGet($"/competitions/{{competitionId:guid}}/ranking/export/{format}",
                async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
                Guid competitionId,
                [FromServices] IRankingPublication? publication,
                CancellationToken cancellationToken) =>
            {
                if (publication is null)
                {
                    return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
                }

                var export = await publication.ExportAsync(competitionId, cancellationToken);
                if (export is null)
                {
                    return TypedResults.Problem(RankingEndpoints.CompetitionNotFound, statusCode: 404);
                }

                // "1/2026" carries a slash no file system takes in a name.
                var fileName = $"lista-rankingowa-{export.CompetitionNumber.Replace('/', '-')}.{format}";
                return TypedResults.File(write(export), contentType, fileName);
            })
                .WithName($"ExportRanking{char.ToUpperInvariant(format[0])}{format[1..]}")
                .WithSummary($"The ranking list as a {format.ToUpperInvariant()} file, draft or approved.")
                .ProducesProblem(StatusCodes.Status404NotFound)
                .RequireAuthorization(operatorPolicy);
        }

        app.MapGet("/public/competitions/{competitionId:guid}/results",
            async Task<Results<Ok<PublicResultsResponse>, ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IRankingPublication? publication,
            CancellationToken cancellationToken) =>
        {
            if (publication is null)
            {
                return TypedResults.Problem(EvaluationEndpoints.Unavailable, statusCode: 503);
            }

            // 404 before approval, the same as for no competition at all:
            // a draft decision must not be guessable from the answer.
            var results = await publication.PublishedAsync(competitionId, cancellationToken);
            return results is null
                ? TypedResults.Problem(NoResults, statusCode: 404)
                : TypedResults.Ok(results);
        })
            .WithName("GetPublicResults")
            .WithSummary("The approved results of a competition: funded applications and the reserve list, without an account.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }
}
