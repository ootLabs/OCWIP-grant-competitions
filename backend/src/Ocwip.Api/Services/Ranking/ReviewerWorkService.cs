using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Ranking;

/// <summary>The applications assigned to one expert, with their own cards (T-40).</summary>
internal interface IReviewerWorkService
{
    Task<ReviewerWorkResponse> ForAsync(Guid reviewerId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads only through the caller's own active assignments, so the list can
/// never show an application the expert may not open (T-37), and only the
/// caller's own cards, never another expert's (T-38, independence).
/// </summary>
internal sealed class ReviewerWorkService(AppDbContext context) : IReviewerWorkService
{
    public async Task<ReviewerWorkResponse> ForAsync(Guid reviewerId, CancellationToken cancellationToken)
    {
        var applications = await context.Applications.AsNoTracking()
            .Where(x => x.IsActive
                && x.Status != ApplicationStatus.Draft
                && context.ApplicationAssignments.Any(
                    a => a.ApplicationId == x.Id && a.ReviewerId == reviewerId && a.IsActive))
            .Include(x => x.Competition)
            .Include(x => x.Entity)
            .ToListAsync(cancellationToken);

        var ids = applications.Select(x => x.Id).ToList();

        var cards = await context.Evaluations.AsNoTracking()
            .Where(x => ids.Contains(x.ApplicationId)
                && x.Stage == EvaluationStage.Merit
                && x.AuthorUserId == reviewerId
                && x.IsActive)
            .ToDictionaryAsync(x => x.ApplicationId, cancellationToken);

        var versionIds = applications.Select(x => x.FormDefinitionId)
            .Concat(cards.Values.Select(x => x.FormDefinitionId))
            .Distinct()
            .ToList();

        var documents = await context.FormDefinitions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => FormSchemaValidator.Validate(x.Definition, x.Purpose).Document,
                cancellationToken);

        var rows = applications.Select(application =>
        {
            documents.TryGetValue(application.FormDefinitionId, out var form);
            var values = form is null
                ? new ApplicationRoleValues(null, null, null)
                : ApplicationRoleValues.Read(form, application.Answers);

            cards.TryGetValue(application.Id, out var card);
            decimal? recommended = null;

            if (card is not null && documents.TryGetValue(card.FormDefinitionId, out var cardDocument) && cardDocument is not null)
            {
                recommended = EvaluationScores.Read(cardDocument, card.Answers, application.Entity.Type).RecommendedGrant;
            }

            var standing = card is null
                ? OwnCardStanding.NotStarted
                : card.Status == EvaluationStatus.Finished ? OwnCardStanding.Finished : OwnCardStanding.Draft;

            return (application.Competition, Row: new ReviewerApplication(
                application.Id,
                application.Number,
                application.Entity.Type,
                values.ProjectTitle,
                values.RequestedGrant,
                standing,
                card?.Id,
                recommended));
        });

        var competitions = rows
            .GroupBy(row => row.Competition.Id)
            .Select(group =>
            {
                var competition = group.First().Competition;
                var list = group.Select(row => row.Row)
                    .OrderBy(row => row.Number?.Length ?? int.MaxValue)
                    .ThenBy(row => row.Number)
                    .ToList();

                return new ReviewerCompetition(
                    competition.Id,
                    competition.Number,
                    competition.Title,
                    competition.TotalPoolAmount,
                    list.Sum(row => row.RequestedGrant ?? 0m),
                    list.Sum(row => row.RecommendedGrant ?? 0m),
                    list);
            })
            .OrderBy(competition => competition.Number)
            .ToList();

        return new ReviewerWorkResponse(competitions);
    }
}
