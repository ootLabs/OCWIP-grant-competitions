using Ocwip.Api.Contracts;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

/// <summary>
/// How a request about a form version ended. One value per thing the caller
/// can do about it, which is one status code per value at the endpoint.
/// </summary>
internal enum FormDefinitionOutcome
{
    Succeeded,

    /// <summary>
    /// No such competition. Separate from the one below although both answer
    /// 404, because the message differs: telling an operator who mistyped the
    /// competition that there is no such VERSION sends them looking at the
    /// wrong thing.
    /// </summary>
    CompetitionNotFound,

    /// <summary>
    /// The competition exists, but nobody ever published that version of its
    /// form.
    /// </summary>
    NotFound,

    /// <summary>
    /// The competition is marked inactive, so nothing is published under it.
    /// Same rule as editing the competition itself.
    /// </summary>
    Inactive,

    /// <summary>
    /// The document did not pass the contract gate of T-24. The reasons travel
    /// with the result, because a refusal that does not name the field leaves
    /// the operator guessing.
    /// </summary>
    InvalidDefinition,

    /// <summary>
    /// Two publications raced and the unique index on
    /// (competition_id, version_number) answered first. Retrying works, which
    /// is exactly why this is its own outcome and not a 500.
    /// </summary>
    VersionTaken
}

internal sealed record FormDefinitionResult(
    FormDefinitionOutcome Outcome,
    FormDefinitionResponse? Definition = null,
    IReadOnlyList<FormSchemaError>? Errors = null);

/// <summary>
/// Versions of the form of a competition (T-25).
///
/// The shape of this interface is the card: there is a publish, there are two
/// reads, and there is deliberately NO update and NO delete. A version that
/// applications already point at is a record of what somebody was shown, and
/// retention is five years, so the only legal way to change a form is to
/// publish the next version of it.
/// </summary>
internal interface IFormDefinitionService
{
    /// <summary>
    /// Stores the document as the next version of the form of this
    /// competition, and makes it the version new applications start from. It
    /// never touches an application that already exists: an applicant halfway
    /// through a draft keeps the fields they were given.
    /// </summary>
    Task<FormDefinitionResult> PublishAsync(
        Guid competitionId,
        FormDefinitionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every version of the form of this competition, oldest first, without
    /// the documents. Null when there is no such competition, which the caller
    /// turns into a 404.
    /// </summary>
    Task<IReadOnlyList<FormDefinitionSummaryResponse>?> ListAsync(
        Guid competitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// One version with its document, addressed by the version number rather
    /// than by an identifier, because the number is what a person reads off a
    /// submitted application.
    /// </summary>
    Task<FormDefinitionResult> GetAsync(
        Guid competitionId,
        int versionNumber,
        CancellationToken cancellationToken);
}
