using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// How a request to this service ended. One value per thing the caller can do
/// about it, which is also one status code per value at the endpoint.
/// </summary>
internal enum CompetitionOutcome
{
    Succeeded,

    /// <summary>
    /// No such competition, or the caller is not allowed to know there is one.
    /// A draft asked for through a public route answers with this, which is
    /// the point: a 403 there would confirm that the guessed identifier names
    /// something real.
    /// </summary>
    NotFound,

    /// <summary>
    /// Another competition already carries this number.
    /// </summary>
    NumberTaken,

    /// <summary>
    /// The move is not in the transition table for an operator. The request
    /// was well formed, so this is 409 and not 400: nothing about the body
    /// needs fixing, the competition is simply not in a state it can be made
    /// from.
    /// </summary>
    TransitionNotAllowed,

    /// <summary>
    /// The form version does not exist, or belongs to another competition. The
    /// composite foreign key would catch the second case anyway; catching it
    /// here is the difference between a message and a 500.
    /// </summary>
    UnknownFormDefinition,

    /// <summary>
    /// A contact person named in step 1.6 is not a staff account: the id has
    /// no account behind it, or the account behind it is not an operator. A
    /// competition page published with an applicant's address on it as the
    /// person to ask would be a leak we wrote ourselves.
    /// </summary>
    UnknownContact,

    /// <summary>
    /// The competition is marked inactive. Nothing is edited or moved through
    /// the lifecycle in that state: the row is kept for the retention period,
    /// not to carry on being worked on.
    /// </summary>
    Inactive
}

/// <param name="CurrentStatus">
/// Where the competition actually is, filled in on TransitionNotAllowed so the
/// message can name it. D12: a validation message carries the value that
/// decided it, not the rule that rejected it.
/// </param>
internal sealed record CompetitionResult(
    CompetitionOutcome Outcome,
    CompetitionResponse? Competition = null,
    CompetitionStatus? CurrentStatus = null);

/// <summary>
/// Everything an operator does to a competition, and the two reads a guest is
/// allowed (T-20).
///
/// Internal like the other services here, exposed to the test project through
/// InternalsVisibleTo: nothing outside the API needs to name these types, and
/// the generated TypeScript client is built from the CONTRACTS, not from this.
/// </summary>
internal interface ICompetitionService
{
    Task<CompetitionResult> CreateAsync(
        CompetitionRequest request,
        CancellationToken cancellationToken);

    Task<CompetitionResult> UpdateAsync(
        Guid id,
        CompetitionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the competition one step through the lifecycle, if the transition
    /// table allows an operator to make that step from where it is now.
    /// </summary>
    Task<CompetitionResult> ChangeStatusAsync(
        Guid id,
        CompetitionStatus target,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the competition inactive. There is no hard delete: AGENTS.md,
    /// security rule 5, keeps the documentation for at least five years.
    /// Idempotent, because asking for the state something is already in is not
    /// an error.
    /// </summary>
    Task<CompetitionResult> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<CompetitionResult> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every competition, drafts and inactive ones included. An operator is
    /// the one caller entitled to the whole board.
    /// </summary>
    Task<IReadOnlyList<CompetitionResponse>> ListAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PublicCompetitionResponse>> ListPublicAsync(
        CancellationToken cancellationToken);

    Task<PublicCompetitionResponse?> GetPublicAsync(
        Guid id,
        CancellationToken cancellationToken);
}
