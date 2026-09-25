using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

/// <summary>Who can evaluate and who is assigned where, for the operator (T-41).</summary>
internal interface IReviewerDirectory
{
    Task<IReadOnlyList<ReviewerSummary>> ReviewersAsync(CancellationToken cancellationToken);

    /// <summary>Null when there is no such competition.</summary>
    Task<IReadOnlyList<CompetitionAssignment>?> AssignmentsAsync(Guid competitionId, CancellationToken cancellationToken);
}

internal sealed class ReviewerDirectory(AppDbContext context) : IReviewerDirectory
{
    /// <summary>Active accounts with the Reviewer role, the same rule ApplicationAssignmentService assigns by.</summary>
    public async Task<IReadOnlyList<ReviewerSummary>> ReviewersAsync(CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(u => u.Role == Role.Reviewer && u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new ReviewerSummary(u.Id, (u.FirstName + " " + u.LastName).Trim(), u.Email ?? string.Empty))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CompetitionAssignment>?> AssignmentsAsync(
        Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return null;
        }

        return await context.ApplicationAssignments.AsNoTracking()
            .Where(a => a.IsActive && a.Application.CompetitionId == competitionId)
            .Select(a => new CompetitionAssignment(a.ApplicationId, a.ReviewerId))
            .ToListAsync(cancellationToken);
    }
}
