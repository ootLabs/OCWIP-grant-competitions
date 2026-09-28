using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Templates of attachment requirements (T-102): the operator uploads,
/// replaces and withdraws the file of a requirement; anybody downloads the
/// one in force while the competition is public.
/// </summary>
public static class AttachmentTemplateEndpoints
{
    internal const string NotFound = "Nie ma takiego wymogu załącznika.";
    internal const string NoTemplate = "Ten załącznik nie ma wzoru do pobrania.";

    public static void MapAttachmentTemplateEndpoints(this WebApplication app)
    {
        var operatorPolicy = AuthorizationConfiguration.Names.For(Role.Operator);

        app.MapPut("/competition-attachments/{requirementId:guid}/template",
            async Task<Results<Ok<AttachmentTemplateResponse>, ProblemHttpResult>> (
            Guid requirementId,
            IFormFile file,
            [FromServices] IAttachmentTemplateService? templates,
            CancellationToken cancellationToken) =>
        {
            if (templates is null)
            {
                return TypedResults.Problem(AttachmentEndpoints.Unavailable, statusCode: 503);
            }

            await using var stream = file.OpenReadStream();
            var result = await templates.UploadAsync(requirementId, file.FileName, stream, cancellationToken);

            return result.Outcome switch
            {
                AttachmentTemplateOutcome.Succeeded => TypedResults.Ok(result.Template!),
                AttachmentTemplateOutcome.EmptyFile => TypedResults.Problem(AttachmentEndpoints.EmptyFile, statusCode: 400),
                AttachmentTemplateOutcome.FileTooLarge => TypedResults.Problem(result.Message, statusCode: 400),
                AttachmentTemplateOutcome.UnsupportedFormat => TypedResults.Problem(AttachmentEndpoints.UnsupportedFormat, statusCode: 400),
                _ => TypedResults.Problem(NotFound, statusCode: 404),
            };
        })
            .WithName("UploadAttachmentTemplate")
            .WithSummary("Uploads the template of a requirement, replacing the one in force. The format is decided by the bytes; 10 MB at most.")
            .DisableAntiforgery()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapPost("/competition-attachments/{requirementId:guid}/template/withdraw",
            async Task<Results<NoContent, ProblemHttpResult>> (
            Guid requirementId,
            [FromServices] IAttachmentTemplateService? templates,
            CancellationToken cancellationToken) =>
        {
            if (templates is null)
            {
                return TypedResults.Problem(AttachmentEndpoints.Unavailable, statusCode: 503);
            }

            var result = await templates.WithdrawAsync(requirementId, cancellationToken);
            return result.Outcome is AttachmentTemplateOutcome.Succeeded
                ? TypedResults.NoContent()
                : TypedResults.Problem(NotFound, statusCode: 404);
        })
            .WithName("WithdrawAttachmentTemplate")
            .WithSummary("Withdraws the template of a requirement: no longer offered, the file itself kept.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(operatorPolicy);

        app.MapGet("/public/attachment-templates/{requirementId:guid}",
            async Task<Results<FileStreamHttpResult, ProblemHttpResult>> (
            Guid requirementId,
            [FromServices] IAttachmentTemplateService? templates,
            CancellationToken cancellationToken) =>
        {
            if (templates is null)
            {
                return TypedResults.Problem(AttachmentEndpoints.Unavailable, statusCode: 503);
            }

            var download = await templates.DownloadPublicAsync(requirementId, cancellationToken);

            // Always as a download, with the type of the verified format, as
            // for an applicant's attachment.
            return download is null
                ? TypedResults.Problem(NoTemplate, statusCode: 404)
                : TypedResults.File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
        })
            .WithName("DownloadAttachmentTemplate")
            .WithSummary("The template in force of a requirement of a public competition, without signing in.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }
}
