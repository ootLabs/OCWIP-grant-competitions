using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// A competition as an operator sees it (T-20).
/// </summary>
/// <param name="Status">
/// The EFFECTIVE state, not the one in the column: the stored value advanced
/// by every scheduled transition whose moment has passed. See
/// CompetitionLifecycle for why the column is not rewritten instead.
/// </param>
/// <param name="AllowedTransitions">
/// Where an operator may go from here, straight out of the transition table.
/// Sent so that a panel draws its buttons from the rule rather than from a
/// copy of the rule, which is the copy that goes stale first.
/// </param>
public sealed record CompetitionResponse(
    Guid Id,
    string Number,
    string Title,
    string? Description,
    CompetitionStatus Status,
    IReadOnlyList<CompetitionStatus> AllowedTransitions,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    bool IsContinuousIntake,
    decimal MaxGrantAmount,
    Guid? FormDefinitionId,
    DateTimeOffset? PublishedAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
