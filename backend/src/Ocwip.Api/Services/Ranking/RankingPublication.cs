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
            .Where(row => row.Status is ApplicationStatus.Funded or ApplicationStatus.Reserve)
            .Select(row => new PublicResultRow(
                row.Rank, row.Number, row.EntityName, row.ProjectTitle, row.TotalScore,
                row.Status == ApplicationStatus.Funded ? row.AwardedGrant : null, row.Status))
            .ToList();

        return new PublicResultsResponse(
            competitionId, competition.Number, competition.Title, competition.ResultsApprovedAt!.Value, rows);
    }
}
