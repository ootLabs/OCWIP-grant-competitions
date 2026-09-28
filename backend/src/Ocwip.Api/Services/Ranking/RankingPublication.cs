using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;

namespace Ocwip.Api.Services.Ranking;

/// <summary>The ranking list leaving the system: exported for the operator and published for everybody (T-42a).</summary>
internal interface IRankingPublication
{
    /// <summary>Every row, draft or approved, for the operator's files; null for no such competition.</summary>
    Task<RankingExport?> ExportAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>Null until the results are approved, and for a competition that is not public.</summary>
    Task<PublicResultsResponse?> PublishedAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>Every resolved competition with its funded projects, the latest approval first (T-108).</summary>
    Task<IReadOnlyList<ResultsArchiveEntry>> ArchiveAsync(CancellationToken cancellationToken);
}

internal sealed class RankingPublication(AppDbContext context, IRankingService ranking) : IRankingPublication
{
    public async Task<RankingExport?> ExportAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId)
            .Select(x => new { x.Number, x.Title })
            .FirstOrDefaultAsync(cancellationToken);
        var list = await ranking.GetRankingAsync(competitionId, cancellationToken);

        return competition is null || list.Ranking is null
            ? null
            : RankingExport.From(competition.Number, competition.Title, list.Ranking);
    }

    public async Task<PublicResultsResponse?> PublishedAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId && x.IsActive && x.ResultsApprovedAt != null)
            .Select(x => new { x.Number, x.Title, x.ResultsApprovedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (competition is null)
        {
            return null;
        }

        var list = await ranking.GetRankingAsync(competitionId, cancellationToken);
        if (list.Ranking is null)
        {
            return null;
        }

        var rows = list.Ranking.Rows
            .Where(row => ApplicationStatuses.IsGranted(row.Status) || row.Status == ApplicationStatus.Reserve)
            .Select(row => new PublicResultRow(
                row.Rank, row.Number, row.EntityName, row.ProjectTitle, row.TotalScore,
                ApplicationStatuses.IsGranted(row.Status) ? row.AwardedGrant : null,
                // The public list says "funded" whether or not the contract is signed yet.
                ApplicationStatuses.IsGranted(row.Status) ? ApplicationStatus.Funded : row.Status))
            .ToList();

        return new PublicResultsResponse(
            competitionId, competition.Number, competition.Title, competition.ResultsApprovedAt!.Value, rows);
    }

    /// <summary>
    /// Built from <see cref="PublishedAsync"/> competition by competition, so
    /// the archive can never show more than the published results do: the
    /// same approval, the same rows, minus the reserve list, which stops
    /// meaning anything once the money is given out. Resolved and archived
    /// competitions both belong here: "Archiwalny" leaves the current listing,
    /// not the record of who was funded. OCWIP runs a few competitions a year,
    /// so one ranking read per competition is cheap.
    /// </summary>
    public async Task<IReadOnlyList<ResultsArchiveEntry>> ArchiveAsync(CancellationToken cancellationToken)
    {
        var ids = await context.Competitions.AsNoTracking()
            .Where(x => x.IsActive && x.ResultsApprovedAt != null
                && (x.Status == CompetitionStatus.Resolved || x.Status == CompetitionStatus.Archived))
            .OrderByDescending(x => x.ResultsApprovedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var entries = new List<ResultsArchiveEntry>();
        foreach (var id in ids)
        {
            if (await PublishedAsync(id, cancellationToken) is not { } results)
            {
                continue;
            }

            entries.Add(new ResultsArchiveEntry(
                results.CompetitionId, results.CompetitionNumber, results.CompetitionTitle, results.ApprovedAt,
                [.. results.Rows
                    .Where(row => row.Status == ApplicationStatus.Funded)
                    .Select(row => new ArchivedProject(row.EntityName, row.ProjectTitle, row.AwardedGrant))]));
        }

        return entries;
    }
}
