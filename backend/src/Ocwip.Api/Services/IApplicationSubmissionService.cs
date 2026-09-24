using System.Security.Claims;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// How a submission or confirmation PDF request ended. One value per thing
/// the caller can do about it, the same shape IApplicationService and
/// IAttachmentService use and for the same reason: one status code per value
/// at the endpoint.
///
/// Kept separate from ApplicationOutcome (IApplicationService.cs) rather than
/// added to it, even though several values mean the same thing: submission is
/// a distinct module from draft autosave (AGENTS.md, "jedna domena, jeden
/// moduł"), and sharing the enum would mean every change here risks a compile
/// error in T-29's already shipped file for no benefit, since nothing outside
/// this service switches on both at once.
/// </summary>
internal enum ApplicationSubmissionOutcome
{
    Succeeded,

    /// <summary>No application with this id.</summary>
    NotFound,

    /// <summary>
    /// The session's cookie no longer matches a real account. Defensive: the
    /// endpoint already requires authentication and the resource policy
    /// already resolved an owner, so this should not be reachable in
    /// practice, but a submission is exactly the write this product cannot
    /// afford to attribute to nobody.
    /// </summary>
    AccountNotFound,

    /// <summary>The application is not a draft any more.</summary>
    AlreadySubmitted,

    /// <summary>The application was deactivated by its own applicant.</summary>
    Inactive,

    /// <summary>
    /// T-21's rule refused the write: the intake has not opened yet, has
    /// closed, or the competition is not accepting applications at all.
    /// </summary>
    IntakeClosed,

    /// <summary>
    /// The stored answers do not pass the submission level of T-30's
    /// validator: something required is still missing, out of range, or over
    /// a limit. The reasons travel on the result, one per field.
    /// </summary>
    AnswersRejected,

    /// <summary>
    /// The confirmation PDF was asked for before the application was ever
    /// submitted. There is nothing to confirm yet.
    /// </summary>
    NotSubmitted,
}

internal sealed record ApplicationSubmissionResult(
    ApplicationSubmissionOutcome Outcome,
    ApplicationResponse? Application = null,
    string? Message = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>
/// A generated confirmation PDF: the bytes and the file name to offer it
/// under. Null content means the outcome on the same result explains why
/// there is nothing to download.
/// </summary>
internal sealed record ApplicationConfirmationPdfResult(
    ApplicationSubmissionOutcome Outcome,
    byte[]? Content = null,
    string? FileName = null);

/// <summary>
/// Submitting a draft application and downloading its confirmation PDF
/// (T-33). Everything a draft application needs before it stops being one:
/// full validation at the submission level, the T-21 deadline check, freezing
/// the answers, assigning the application number, an append-only status
/// history entry and a confirmation e-mail.
///
/// "Freezing the answers" is not a lock this service writes: the existing
/// PUT /applications/{id} from T-29 already refuses to touch a non-Draft
/// application (ApplicationService.SaveDraftAsync), so the moment this
/// service flips the status, that guard freezes everything for free. Nothing
/// here re-implements it.
/// </summary>
internal interface IApplicationSubmissionService
{
    /// <summary>
    /// Submits a draft: validates it at the submission level, checks the
    /// intake window, assigns the application number, writes the status
    /// history entry and sends the confirmation e-mail. Irreversible: once
    /// this succeeds, the row is Submitted and PUT /applications/{id} refuses
    /// every further edit.
    /// </summary>
    Task<ApplicationSubmissionResult> SubmitAsync(
        Guid id,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken);

    /// <summary>
    /// The confirmation PDF for an already submitted application. Refused for
    /// a draft: there is nothing to confirm before Submitted, and generating
    /// one would print a document that looks final for something that is not.
    /// </summary>
    Task<ApplicationConfirmationPdfResult> GetConfirmationPdfAsync(
        Guid id,
        CancellationToken cancellationToken);
}
