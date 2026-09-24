using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// How an attachment request ended (T-32). One value per outcome the caller
/// can do something about, the same shape IApplicationService uses and for
/// the same reason: one status code per value at the endpoint.
/// </summary>
internal enum AttachmentOutcome
{
    Succeeded,

    /// <summary>No application with this id (upload target).</summary>
    ApplicationNotFound,

    /// <summary>No attachment with this id (replace target).</summary>
    NotFound,

    /// <summary>
    /// T-21's rule refused the write, the same check CreateDraftAsync and
    /// SaveDraftAsync make (R-29): an attachment is part of the application,
    /// so it is bound by the same intake window.
    /// </summary>
    IntakeClosed,

    /// <summary>The application is no longer a draft.</summary>
    AlreadySubmitted,

    /// <summary>The application was deactivated by its own applicant.</summary>
    Inactive,

    /// <summary>
    /// The uploaded bytes are not one of the eight allowed formats, decided
    /// from the file's own signature, never from the declared content type.
    /// </summary>
    UnsupportedFormat,

    /// <summary>The file is empty, which is a failed upload, not a document.</summary>
    EmptyFile,

    /// <summary>The file alone is over the competition's per-file limit.</summary>
    FileTooLarge,

    /// <summary>
    /// Adding this file would put the application's active attachments over
    /// the competition's per-application limit.
    /// </summary>
    ApplicationTooLarge,
}

internal sealed record AttachmentResult(
    AttachmentOutcome Outcome,
    AttachmentResponse? Attachment = null,
    string? Message = null);

/// <summary>
/// A downloadable attachment: the open stream, the name to offer it under,
/// and the content type decided by AttachmentFormatDetector rather than the
/// one the uploader declared, so a browser is never told to render a
/// mislabelled file inline as something it is not (T-32).
/// </summary>
internal sealed record AttachmentDownload(
    Stream Content, string FileName, string ContentType);

/// <summary>
/// Uploading, replacing and downloading the files attached to an application
/// (T-32). Metadata alone is Models/Attachment.cs (T-11.4); this is what reads
/// and writes the bytes.
/// </summary>
internal interface IAttachmentService
{
    /// <summary>
    /// Stores a new attachment under a draft application. Refused once the
    /// competition's intake has closed, the application is no longer a draft,
    /// or the file fails the format or size checks.
    /// </summary>
    Task<AttachmentResult> UploadAsync(
        Guid applicationId,
        string fileName,
        string declaredContentType,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an existing attachment: the new file becomes a new row, and
    /// the one it replaces is marked inactive, never deleted (AGENTS.md rule
    /// 5, and the card's own "poprzedni nie znika twardo"). Subject to the
    /// same checks as UploadAsync.
    /// </summary>
    Task<AttachmentResult> ReplaceAsync(
        Guid attachmentId,
        string fileName,
        string declaredContentType,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens an attachment for download. Null means the row disappeared
    /// between the authorization check and this call, which the endpoint
    /// answers as 404: an ordinary race, not a new outcome to add above.
    /// </summary>
    Task<AttachmentDownload?> DownloadAsync(
        Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The bare resource an authorization check needs (Authorization/
    /// IEntityScoped.cs), the same split IApplicationService makes and for the
    /// same reason.
    /// </summary>
    Task<Attachment?> FindForAuthorizationAsync(
        Guid id, CancellationToken cancellationToken);
}
