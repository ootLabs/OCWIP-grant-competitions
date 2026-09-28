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

/// <summary>
/// One resolved competition in the public results archive (T-108, R-14,
/// R-31): only the funded projects, with who, what and how much. The name is
/// the entity's own, which for an informal group is the group's name: the
/// members' names are never part of it (RD3).
/// </summary>
public sealed record ResultsArchiveEntry(
    Guid CompetitionId,
    string CompetitionNumber,
    string CompetitionTitle,
    DateTimeOffset ApprovedAt,
    IReadOnlyList<ArchivedProject> Projects);

public sealed record ArchivedProject(string EntityName, string? ProjectTitle, decimal? AwardedGrant);
