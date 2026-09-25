using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The submitted applications of one competition, as the operator tracks
/// them (T-35, krok 4.1 in docs/runbook/proces.md). Drafts are never here:
/// a started and unsubmitted application is not something the operator has
/// received.
/// </summary>
/// <param name="RequestedTotal">
/// The sum of the requested grants of the listed applications. An
/// application whose form marks no grant field adds nothing to it.
/// </param>
/// <param name="PoolRemaining">
/// The competition's pool minus <paramref name="RequestedTotal"/>, negative
/// when the applications ask for more than there is. Null when the
/// competition has no pool set: "no pool" is not "a pool of zero".
/// </param>
public sealed record ApplicationListResponse(
    Guid CompetitionId,
    string CompetitionNumber,
    string CompetitionTitle,
    decimal? TotalPoolAmount,
    decimal RequestedTotal,
    decimal? PoolRemaining,
    IReadOnlyList<ApplicationListItem> Applications);

/// <summary>
/// One row of the list. Title, cost and grant come from the fields the form
/// marks with a role (docs/kontrakt-formularza.md, "role") and are null when
/// it marks none.
/// </summary>
public sealed record ApplicationListItem(
    Guid Id,
    string Number,
    string EntityName,
    EntityType EntityType,
    string? ProjectTitle,
    decimal? TotalCost,
    decimal? RequestedGrant,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt);
