using System.Security.Claims;
using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// How a request about a draft application ended. One value per thing the
/// caller can do about it, which is one status code per value at the
/// endpoint.
/// </summary>
internal enum ApplicationOutcome
{
    Succeeded,

    /// <summary>No such competition. Creating a draft against it.</summary>
    CompetitionNotFound,

    /// <summary>
    /// The competition exists but nobody has published a form for it yet, so
    /// there is nothing to start a draft against.
    /// </summary>
    NoFormDefinition,

    /// <summary>
    /// T-21's rule refused the write: the intake has not opened yet, has
    /// closed, or the competition is not accepting applications at all. The
    /// message travels on the result rather than as a fixed string, because it
    /// names the moment that decided it (D12).
    /// </summary>
    IntakeClosed,

    /// <summary>
    /// The calling account has no Podmiot (B-09: every account looks like this
    /// today), so there is nothing to file the application under.
    /// </summary>
    NoEntity,

    /// <summary>No application with this id.</summary>
    NotFound,

    /// <summary>
    /// The application is no longer a draft. Nothing sets that today (T-33 is
    /// what will), but the check stays defensive rather than assuming it can
    /// never happen to a row this service is asked to touch.
    /// </summary>
    AlreadySubmitted,

    /// <summary>
    /// The posted answers are not a JSON object. The column would also take
    /// an array, but the answers are keyed by field (T-30), and an array has
    /// no keys to check. Caught here so the caller gets a 400 naming the
    /// problem instead of the Npgsql serializer's 500.
    /// </summary>
    InvalidAnswers,

    /// <summary>
    /// The answers do not fit the form version the application was started
    /// on (T-30): a key the form does not have, a value of the wrong kind, a
    /// choice that is not on the list. The reasons travel on the result, one
    /// per field, so the form can put each under its own input.
    /// </summary>
    AnswersRejected,

    /// <summary>
    /// The draft has been marked inactive (its own applicant deactivated it,
    /// see DeactivateAsync). Reading it back stays allowed, the card is
    /// explicit that a deactivated draft "zostaje widoczna", but writing to a
    /// row its owner just asked to hide would undo that in one stray autosave.
    /// </summary>
    Inactive,
}

internal sealed record ApplicationResult(
    ApplicationOutcome Outcome,
    ApplicationResponse? Application = null,
    string? Message = null,
    IDictionary<string, string[]>? Errors = null);

internal sealed record ApplicationFormResult(
    ApplicationOutcome Outcome,
    ApplicationFormResponse? Form = null);

/// <summary>
/// Draft applications: starting one, autosaving it, reading it back and
/// marking it inactive (T-29).
///
/// Submission and everything that happens to an application once it stops
/// being a draft belong to T-33, which checks the answers at the submission
/// level of the same validator (Models/Forms/AnswerValidator.cs). This
/// interface is deliberately narrow to what a draft needs.
/// </summary>
internal interface IApplicationService
{
    /// <summary>
    /// Starts an empty draft for the calling account's Podmiot against a
    /// competition's current form. The caller, not an id in the body, decides
    /// whose Podmiot this is: nobody may start a draft under somebody else's.
    /// </summary>
    Task<ApplicationResult> CreateDraftAsync(
        Guid competitionId,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken);

    /// <summary>
    /// Overwrites the stored answers. Refused once the competition's intake
    /// has closed (T-21), once the application is no longer a draft, and for
    /// answers that do not fit the application's own form version at the
    /// draft level of T-30 (gaps allowed, foreign keys and wrong shapes not).
    /// </summary>
    Task<ApplicationResult> SaveDraftAsync(
        Guid id,
        JsonElement answers,
        CancellationToken cancellationToken);

    Task<ApplicationResult> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The form document this application was started on (T-34), at the
    /// version pinned to it, never the competition's current one. Read once
    /// when the fill screen opens, not on every autosave: see
    /// ApplicationFormResponse for why it is not part of ApplicationResponse.
    /// </summary>
    Task<ApplicationFormResult> GetFormDefinitionAsync(
        Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the draft inactive. Never a hard delete (AGENTS.md rule 5): the
    /// row and its answers stay for the retention period.
    /// </summary>
    Task<ApplicationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The bare resource an authorization check needs: enough to ask "whose is
    /// this" (Authorization/IEntityScoped.cs) before anything else runs. Kept
    /// separate from the outcomes above because a failed authorization check
    /// is not one of them, it happens before the service is asked to do
    /// anything.
    /// </summary>
    Task<Application?> FindForAuthorizationAsync(
        Guid id, CancellationToken cancellationToken);
}
