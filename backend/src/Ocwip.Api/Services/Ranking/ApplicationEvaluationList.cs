using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

/// <summary>Every active card of one application, for the operator (T-41a).</summary>
internal interface IApplicationEvaluationList
{
    /// <summary>Null when there is no such active application.</summary>
    Task<IReadOnlyList<ApplicationEvaluationItem>?> ListAsync(Guid applicationId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the cards one by one through IEvaluationService, so each comes with
/// the same card document and scores as when it is opened on its own. An
/// application has the formal card and a merit card per expert, two or
/// three in practice, so the extra reads cost nothing worth a second mapping.
/// </summary>
internal sealed class ApplicationEvaluationList(AppDbContext context, IEvaluationService evaluations)
    : IApplicationEvaluationList
{
    public async Task<IReadOnlyList<ApplicationEvaluationItem>?> ListAsync(
        Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await context.Applications.AnyAsync(x => x.Id == applicationId && x.IsActive, cancellationToken))
        {
            return null;
        }

        var rows = await context.Evaluations.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Stage,
                // A card entered for somebody without an account (AuthorName)
                // keeps that name; otherwise the author's account names them.
                Author = x.AuthorName
                    ?? context.Users.Where(u => u.Id == x.AuthorUserId)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault()
                    ?? string.Empty,
            })
            // Sorted by the database, as ReviewerDirectory does, and not with
            // a "pl-PL" comparer here: an image without ICU data has no such
            // culture (see PolishNumbers).
            .OrderBy(x => x.Stage == EvaluationStage.Formal ? 0 : 1)
            .ThenBy(x => x.Author)
            .ToListAsync(cancellationToken);

        var items = new List<ApplicationEvaluationItem>(rows.Count);

        foreach (var row in rows)
        {
            var result = await evaluations.GetAsync(row.Id, cancellationToken);

            if (result.Evaluation is { } evaluation)
            {
                items.Add(new ApplicationEvaluationItem(evaluation, row.Author));
            }
        }

        return items;
    }
}
