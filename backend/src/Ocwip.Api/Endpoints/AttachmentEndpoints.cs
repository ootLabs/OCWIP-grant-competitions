using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Application attachments over HTTP (T-32): uploading one under a draft,
/// replacing one, and downloading one.
///
/// Download and replace address the attachment directly and carry no role
/// policy of their own, the same order every scoped resource in this product
/// follows (see Endpoints/ApplicationEndpoints.cs): load the row, ask the
/// authorization service, only then act. Upload authorizes against the
/// APPLICATION instead, because the attachment does not exist yet to ask
/// about.
/// </summary>
public static class AttachmentEndpoints
{
    internal const string Unavailable =
        "Obsługa załączników jest chwilowo niedostępna.";

    internal const string ApplicationNotFound = "Nie ma takiego wniosku.";

    internal const string NotFound = "Nie ma takiego załącznika.";

    internal const string AlreadyReplaced =
        "Ten załącznik został już zastąpiony nowszym plikiem.";

    internal const string Forbidden = "Nie masz dostępu do tego załącznika.";

    internal const string ForbiddenApplication = "Nie masz dostępu do tego wniosku.";

    internal const string AlreadySubmitted =
        "Ten wniosek został już złożony, więc nie można zmieniać jego "
        + "załączników.";

    internal const string Inactive =
        "Ten wniosek został usunięty przez wnioskodawcę, więc nie można "
        + "zmieniać jego załączników.";

    internal const string UnsupportedFormat =
        "Niedozwolony format pliku. Dozwolone formaty: PDF, DOC, DOCX, XLS, "
        + "XLSX, JPG, ODT, ODS.";

    internal const string EmptyFile = "Przesłany plik jest pusty.";

    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        app.MapPost("/applications/{applicationId:guid}/attachments",
            async Task<Results<Created<AttachmentResponse>, ProblemHttpResult>> (
            Guid applicationId,
            IFormFile file,
            [FromServices] IAttachmentService? attachments,
            [FromServices] IApplicationService? applications,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (attachments is null || applications is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var resource = await applications.FindForAuthorizationAsync(
                applicationId, cancellationToken);

            if (resource is null)
            {
                return TypedResults.Problem(ApplicationNotFound, statusCode: 404);
            }

            var authorized = await authorization.AuthorizeAsync(
                context.User, resource, AuthorizationConfiguration.Names.OwnsResource);

            if (!authorized.Succeeded)
            {
                return TypedResults.Problem(ForbiddenApplication, statusCode: 403);
            }

            await using var stream = file.OpenReadStream();

            var result = await attachments.UploadAsync(
                applicationId, file.FileName, file.ContentType, stream, cancellationToken);

            return result.Outcome is AttachmentOutcome.Succeeded
                ? TypedResults.Created(
                    $"/attachments/{result.Attachment!.Id}", result.Attachment)
                : Failure(result);
        })
            .WithName("UploadAttachment")
            .WithSummary(
                "Adds a new attachment to a draft application. Rejects a "
                + "format outside the allow list, a file over the "
                + "competition's per-file limit, a total over its "
                + "per-application limit, an already submitted application "
                + "and one whose intake has closed.")
            .DisableAntiforgery()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapGet("/attachments/{id:guid}",
            async Task<Results<FileStreamHttpResult, ProblemHttpResult>> (
            Guid id,
            [FromServices] IAttachmentService? attachments,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (attachments is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                attachments, authorization, context, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            var download = await attachments.DownloadAsync(id, cancellationToken);

            if (download is null)
            {
                return TypedResults.Problem(NotFound, statusCode: 404);
            }

            // Always as an attachment, never inline: the content type comes
            // from the verified format (AttachmentFormatDetector), not from
            // whatever the uploader declared, so a browser is never asked to
            // render a mislabelled file as something it is not.
            return TypedResults.File(
                download.Content,
                download.ContentType,
                download.FileName,
                enableRangeProcessing: false);
        })
            .WithName("DownloadAttachment")
            .WithSummary(
                "Downloads one attachment's bytes. Same permission check as "
                + "the application it belongs to: the owning applicant or an "
                + "operator, nobody else, guessed identifier or not.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();

        app.MapPut("/attachments/{id:guid}",
            async Task<Results<Ok<AttachmentResponse>, ProblemHttpResult>> (
            Guid id,
            IFormFile file,
            [FromServices] IAttachmentService? attachments,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (attachments is null)
            {
                return TypedResults.Problem(Unavailable, statusCode: 503);
            }

            var problem = await AuthorizeAsync(
                attachments, authorization, context, id, cancellationToken);

            if (problem is not null)
            {
                return problem;
            }

            await using var stream = file.OpenReadStream();

            var result = await attachments.ReplaceAsync(
                id, file.FileName, file.ContentType, stream, cancellationToken);

            return result.Outcome is AttachmentOutcome.Succeeded
                ? TypedResults.Ok(result.Attachment!)
                : Failure(result);
        })
            .WithName("ReplaceAttachment")
            .WithSummary(
                "Replaces an attachment with a new file. The previous row is "
                + "marked inactive, never deleted: its bytes stay exactly "
                + "where they were.")
            .DisableAntiforgery()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }

    /// <summary>
    /// Load, then ask, then answer, the same shape ApplicationEndpoints uses:
    /// a row that is not there is 404, a row that is there and is not the
    /// caller's is 403. Null means neither happened and the caller may
    /// proceed.
    /// </summary>
    private static async Task<ProblemHttpResult?> AuthorizeAsync(
        IAttachmentService attachments,
        IAuthorizationService authorization,
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        var resource = await attachments.FindForAuthorizationAsync(id, cancellationToken);

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

    private static ProblemHttpResult Failure(AttachmentResult result) =>
        result.Outcome switch
        {
            AttachmentOutcome.ApplicationNotFound =>
                TypedResults.Problem(ApplicationNotFound, statusCode: 404),

            AttachmentOutcome.NotFound =>
                TypedResults.Problem(NotFound, statusCode: 404),

            AttachmentOutcome.AlreadyReplaced =>
                TypedResults.Problem(AlreadyReplaced, statusCode: 409),

            AttachmentOutcome.IntakeClosed =>
                TypedResults.Problem(result.Message!, statusCode: 409),

            AttachmentOutcome.AlreadySubmitted =>
                TypedResults.Problem(AlreadySubmitted, statusCode: 409),

            AttachmentOutcome.Inactive =>
                TypedResults.Problem(Inactive, statusCode: 409),

            AttachmentOutcome.UnsupportedFormat =>
                TypedResults.Problem(UnsupportedFormat, statusCode: 400),

            AttachmentOutcome.EmptyFile =>
                TypedResults.Problem(EmptyFile, statusCode: 400),

            AttachmentOutcome.FileTooLarge =>
                TypedResults.Problem(result.Message!, statusCode: 400),

            AttachmentOutcome.ApplicationTooLarge =>
                TypedResults.Problem(result.Message!, statusCode: 400),

            // Succeeded never reaches here, and a new outcome should arrive as
            // a visible 500 rather than as a silently successful answer.
            _ => throw new InvalidOperationException(
                $"Unhandled attachment outcome: {result.Outcome}"),
        };
}
