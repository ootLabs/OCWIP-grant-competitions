using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The published results of a competition (T-42a), readable without an
/// account once the operator approved them. Only the funded applications and
/// the reserve list, never the rejected ones (ZR-10): the list announces who
/// gets money and who is next, and a rejection is the applicant's own
/// business, already in their panel.
/// </summary>
public sealed record PublicResultsResponse(
    Guid CompetitionId,
    string CompetitionNumber,
    string CompetitionTitle,
    DateTimeOffset ApprovedAt,
    IReadOnlyList<PublicResultRow> Rows);

public sealed record PublicResultRow(
    int? Rank,
    string? Number,
    string EntityName,
    string? ProjectTitle,
    decimal? TotalScore,
    decimal? AwardedGrant,
    ApplicationStatus Status);
