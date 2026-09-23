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
    /// The posted answers are neither an object nor an array, the same shape
    /// rule the column itself enforces. Caught here so the caller gets a 400
    /// naming the problem instead of the Npgsql serializer's 500.
    /// </summary>
    InvalidAnswers,

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
    string? Message = null);

/// <summary>
/// Draft applications: starting one, autosaving it, reading it back and
/// marking it inactive (T-29).
///
/// Submission, validation against the form and everything that happens to an
/// application once it stops being a draft belong to T-30 and T-33. This
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
    /// has closed (T-21) or the application is no longer a draft.
    /// </summary>
    Task<ApplicationResult> SaveDraftAsync(
        Guid id,
        JsonElement answers,
        CancellationToken cancellationToken);

    Task<ApplicationResult> GetAsync(Guid id, CancellationToken cancellationToken);

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
