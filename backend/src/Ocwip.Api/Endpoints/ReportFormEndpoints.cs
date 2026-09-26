using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Versions of the report form of a competition (T-50a), the same versioning
/// as the application form and the evaluation cards. Operator only.
/// </summary>
public static class ReportFormEndpoints
{
    public static void MapReportFormEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPost("/competitions/{competitionId:guid}/report-form",
            async Task<Results<
                Created<FormDefinitionResponse>,
                ValidationProblem,
                ProblemHttpResult>> (
            Guid competitionId,
            FormDefinitionRequest request,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            var purpose = FormPurpose.Report;

            var result = await definitions.PublishAsync(
                competitionId, purpose, request, cancellationToken);

            if (result.Outcome is FormDefinitionOutcome.InvalidDefinition)
            {
                return TypedResults.ValidationProblem(FormDefinitionEndpoints.Problems(result.Errors!));
            }

            if (result.Outcome is not FormDefinitionOutcome.Succeeded)
            {
                return FormDefinitionEndpoints.Failure(result);
            }

            var published = result.Definition!;
            return TypedResults.Created(
                $"/competitions/{competitionId}/report-form/{published.VersionNumber}",
                published);
        })
            .WithName("PublishReportForm")
            .WithSummary(
                "Publishes the next version of the report form. Reports already "
                + "started keep the version they were started on.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/report-form",
            async Task<Results<
                Ok<IReadOnlyList<FormDefinitionSummaryResponse>>,
                ProblemHttpResult>> (
            Guid competitionId,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            var purpose = FormPurpose.Report;

            var versions = await definitions.ListAsync(competitionId, purpose, cancellationToken);

            return versions is null
                ? TypedResults.Problem(FormDefinitionEndpoints.CompetitionNotFound, statusCode: 404)
                : TypedResults.Ok(versions);
        })
            .WithName("ListReportForms")
            .WithSummary("Every published version of the report form, oldest first.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/competitions/{competitionId:guid}/report-form/{version:int}",
            async Task<Results<Ok<FormDefinitionResponse>, ProblemHttpResult>> (
            Guid competitionId,
            int version,
            [FromServices] IFormDefinitionService? definitions,
            CancellationToken cancellationToken) =>
        {
            if (definitions is null)
            {
                return TypedResults.Problem(FormDefinitionEndpoints.Unavailable, statusCode: 503);
            }

            var purpose = FormPurpose.Report;

            var result = await definitions.GetAsync(competitionId, purpose, version, cancellationToken);

            return result.Outcome is FormDefinitionOutcome.Succeeded
                ? TypedResults.Ok(result.Definition!)
                : FormDefinitionEndpoints.Failure(result);
        })
            .WithName("GetReportForm")
            .WithSummary("One version of the report form with its document.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(operatorPolicy);
    }
}
