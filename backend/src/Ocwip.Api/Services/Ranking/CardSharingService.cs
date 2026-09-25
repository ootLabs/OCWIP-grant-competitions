using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

internal enum CardSharingOutcome
{
    Succeeded,
    CompetitionNotFound,
    AlreadyShared,
}

internal sealed record CardSharingResult(CardSharingOutcome Outcome, CardSharingResponse? Sharing = null);

/// <summary>Sharing the evaluation cards with the applicants (T-41b, report step 5.5).</summary>
internal interface ICardSharingService
{
    Task<CardSharingResult> GetAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>Once per competition; a second call is AlreadyShared, not a new date.</summary>
    Task<CardSharingResult> ShareAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>
    /// The finished cards of one application without anything about their
    /// authors, or null when there is no such active application. The caller
    /// has already been checked as its owner.
    /// </summary>
    Task<ApplicantEvaluationCards?> ForApplicantAsync(Guid applicationId, CancellationToken cancellationToken);
}

internal sealed class CardSharingService(AppDbContext context, IEvaluationService evaluations, TimeProvider time)
    : ICardSharingService
{
    public async Task<CardSharingResult> GetAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId)
            .Select(x => new { x.EvaluationCardsSharedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return competition is null
            ? new CardSharingResult(CardSharingOutcome.CompetitionNotFound)
            : new CardSharingResult(CardSharingOutcome.Succeeded, new CardSharingResponse(competition.EvaluationCardsSharedAt));
    }

    public async Task<CardSharingResult> ShareAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        // One conditional UPDATE, so two operators confirming in the same
        // moment cannot both succeed with two different dates. It bypasses
        // SaveChanges, so the audit timestamp is set here, with the same
        // clock AppDbContext stamps every other change with.
        var changed = await context.Competitions
            .Where(x => x.Id == competitionId && x.IsActive && x.EvaluationCardsSharedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.EvaluationCardsSharedAt, now)
                    .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (changed == 1)
        {
            return new CardSharingResult(CardSharingOutcome.Succeeded, new CardSharingResponse(now));
        }

        var existing = await GetAsync(competitionId, cancellationToken);
        return existing.Outcome is CardSharingOutcome.Succeeded && existing.Sharing!.SharedAt is not null
            ? new CardSharingResult(CardSharingOutcome.AlreadyShared, existing.Sharing)
            : new CardSharingResult(CardSharingOutcome.CompetitionNotFound);
    }

    public async Task<ApplicantEvaluationCards?> ForApplicantAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await context.Applications.AsNoTracking()
            .Where(x => x.Id == applicationId && x.IsActive)
            .Select(x => new { x.Competition.EvaluationCardsSharedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (application is null)
        {
            return null;
        }

        if (application.EvaluationCardsSharedAt is null)
        {
            return new ApplicantEvaluationCards(false, []);
        }

        // Finished cards only: a draft is the evaluator's work in progress,
        // not an evaluation, and could still change after the applicant read it.
        var ids = await context.Evaluations.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.IsActive && x.Status == EvaluationStatus.Finished)
            .OrderBy(x => x.Stage == EvaluationStage.Formal ? 0 : 1)
            .ThenBy(x => x.FinishedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var cards = new List<ApplicantEvaluationCard>(ids.Count);

        foreach (var id in ids)
        {
            if ((await evaluations.GetAsync(id, cancellationToken)).Evaluation is { } e)
            {
                cards.Add(new ApplicantEvaluationCard(
                    e.Stage, e.ApplicantType, e.CardDefinition, e.Answers,
                    e.FormalPassed, e.MeritScore, e.StrategicScore, e.RecommendedGrant));
            }
        }

        return new ApplicantEvaluationCards(true, cards);
    }
}
