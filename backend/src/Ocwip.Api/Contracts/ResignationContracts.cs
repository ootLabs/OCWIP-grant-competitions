namespace Ocwip.Api.Contracts;

/// <summary>A funded application still without a signed contract (T-109).</summary>
/// <param name="Overdue">True once the 14 days from the publication of the results have passed.</param>
public sealed record UnsignedContract(
    Guid ApplicationId,
    string? Number,
    string EntityName,
    decimal? AwardedGrant,
    bool Overdue,
    DateTimeOffset Deadline);

/// <summary>
/// A resignation or a promotion done. MailSent is false when the change is
/// stored but the mail to the applicant did not go out: the operator lets
/// them know another way, since a repeated action would find the new status.
/// </summary>
public sealed record ResignationActionResponse(bool MailSent);

/// <summary>The first application on the reserve list, and what the pool would give it.</summary>
/// <param name="ProposedGrant">The requested amount, or what is left of the pool when that is less; null without a requested amount.</param>
public sealed record ReserveCandidate(
    Guid ApplicationId,
    int? Rank,
    string? Number,
    string EntityName,
    decimal? RequestedGrant,
    decimal? ProposedGrant);

/// <summary>
/// GET /competitions/{id}/resignations (T-109): the contract deadline of the
/// competition, the funded applications without a signed contract, the pool
/// and what is left of it, and the reserve application next in line.
/// </summary>
/// <param name="ContractDeadline">The results' approval plus 14 days; null before the approval.</param>
/// <param name="FreePool">The pool less every amount still granted; null without a pool.</param>
public sealed record ResignationsResponse(
    Guid CompetitionId,
    DateTimeOffset? ResultsApprovedAt,
    DateTimeOffset? ContractDeadline,
    decimal? TotalPool,
    decimal AwardedTotal,
    decimal? FreePool,
    IReadOnlyList<UnsignedContract> Unsigned,
    ReserveCandidate? NextReserve);

/// <summary>POST /applications/{id}/promotion: the amount granted to the reserve application.</summary>
public sealed record PromotionRequest(decimal? AwardedGrant);
