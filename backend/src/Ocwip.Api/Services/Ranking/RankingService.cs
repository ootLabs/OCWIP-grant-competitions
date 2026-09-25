using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Ranking;

/// <summary>
/// Evaluation settings and the ranking list (T-39). The list is computed on
/// every read from the evaluations' answers, never stored: a stored rank
/// would go stale the moment an expert finishes a card.
/// </summary>
internal sealed class RankingService : IRankingService
{
    /// <summary>A committee larger than this is a typo, not a committee.</summary>
    internal const int MaxEvaluators = 20;

    private readonly AppDbContext _context;

    public RankingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RankingResult> GetSettingsAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        return competition is null
            ? new RankingResult(RankingOutcome.CompetitionNotFound)
            : new RankingResult(RankingOutcome.Succeeded, Settings: Settings(competition));
    }

    public async Task<RankingResult> UpdateSettingsAsync(
        Guid competitionId, EvaluationSettingsRequest request, CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new RankingResult(RankingOutcome.CompetitionNotFound);
        }

        if (!competition.IsActive)
        {
            return new RankingResult(RankingOutcome.Inactive);
        }

        var errors = new Dictionary<string, string[]>();

        if (request.EvaluatorsPerApplication is < 1 or > MaxEvaluators)
        {
            errors["evaluatorsPerApplication"] =
                [$"Liczba oceniających jeden wniosek musi mieścić się między 1 a {MaxEvaluators}."];
        }

        if (!Enum.IsDefined(request.ScoreAggregation))
        {
            errors["scoreAggregation"] = ["Nieznany sposób liczenia wyniku."];
        }

        if (request.MeritThreshold is < 0m)
        {
            errors["meritThreshold"] = ["Próg punktowy nie może być ujemny."];
        }

        if (request.DivergenceThresholdPercent is < 0m or > 100m)
        {
            errors["divergenceThresholdPercent"] = ["Próg rozbieżności to procent od 0 do 100."];
        }

        if (errors.Count > 0)
        {
            return new RankingResult(RankingOutcome.InvalidSettings, Errors: errors);
        }

        competition.EvaluatorsPerApplication = request.EvaluatorsPerApplication;
        competition.ScoreAggregation = request.ScoreAggregation;
        competition.MeritThreshold = request.MeritThreshold;
        competition.ThresholdIncludesStrategic = request.ThresholdIncludesStrategic;
        competition.DivergenceThresholdPercent = request.DivergenceThresholdPercent;
        await _context.SaveChangesAsync(cancellationToken);

        return new RankingResult(RankingOutcome.Succeeded, Settings: Settings(competition));
    }

    public async Task<RankingResult> GetRankingAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new RankingResult(RankingOutcome.CompetitionNotFound);
        }

        var applications = await _context.Applications.AsNoTracking()
            .Include(x => x.Entity)
            .Where(x => x.CompetitionId == competitionId
                && x.IsActive
                && x.Status != ApplicationStatus.Draft)
            .ToListAsync(cancellationToken);

        var evaluations = await _context.Evaluations.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.IsActive)
            .ToListAsync(cancellationToken);

        // One read and one parse per version, forms and cards alike, the
        // way the operator's list of applications does it (T-35).
        var versionIds = applications.Select(x => x.FormDefinitionId)
            .Concat(evaluations.Select(x => x.FormDefinitionId))
            .Distinct()
            .ToList();

        var documents = await _context.FormDefinitions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => FormSchemaValidator.Validate(x.Definition, x.Purpose).Document,
                cancellationToken);

        var byApplication = evaluations.ToLookup(x => x.ApplicationId);

        var inputs = applications.Select(application =>
        {
            documents.TryGetValue(application.FormDefinitionId, out var form);
            var values = form is null
                ? new ApplicationRoleValues(null, null, null)
                : ApplicationRoleValues.Read(form, application.Answers);

            var own = byApplication[application.Id].ToList();
            var type = application.Entity.Type;

            var merit = own
                .Where(x => x.Stage == EvaluationStage.Merit && x.Status == EvaluationStatus.Finished)
                .Select(x => Scores(documents, x, type))
                .Where(scores => scores is not null)
                .Select(scores => scores!)
                .ToList();

            var meritCard = own.FirstOrDefault(x => x.Stage == EvaluationStage.Merit) is { } anyMerit
                && documents.TryGetValue(anyMerit.FormDefinitionId, out var card)
                && card is not null
                    ? RankingCalculator.MeritScale(card)
                    : null;

            return new RankingInput(
                application.Id,
                application.Number,
                application.Entity.Name,
                type,
                values.ProjectTitle,
                values.RequestedGrant,
                application.SubmittedAt,
                Formal(documents, own.FirstOrDefault(x => x.Stage == EvaluationStage.Formal), type),
                merit,
                meritCard,
                application.Status,
                application.AwardedGrant,
                application.DecisionNote);
        });

        var rows = RankingCalculator.Rank(inputs, competition);

        return new RankingResult(
            RankingOutcome.Succeeded,
            new RankingResponse(
                competitionId,
                Settings(competition),
                rows,
                competition.TotalPoolAmount,
                rows.Sum(row => row.AwardedGrant ?? 0m),
                competition.ResultsApprovedAt));
    }

    private static EvaluationScores? Scores(
        IReadOnlyDictionary<Guid, FormDocument?> documents, Evaluation evaluation, EntityType applicant) =>
        documents.TryGetValue(evaluation.FormDefinitionId, out var card) && card is not null
            ? EvaluationScores.Read(card, evaluation.Answers, applicant)
            : null;

    private static FormalStanding Formal(
        IReadOnlyDictionary<Guid, FormDocument?> documents, Evaluation? formal, EntityType applicant)
    {
        if (formal is null)
        {
            return FormalStanding.NotStarted;
        }

        if (formal.Status != EvaluationStatus.Finished)
        {
            return FormalStanding.InProgress;
        }

        return Scores(documents, formal, applicant)?.FormalPassed switch
        {
            true => FormalStanding.Passed,
            false => FormalStanding.Failed,
            _ => FormalStanding.InProgress,
        };
    }

    internal static EvaluationSettingsResponse Settings(Competition competition) =>
        new(
            competition.EvaluatorsPerApplication,
            competition.ScoreAggregation,
            competition.MeritThreshold,
            competition.ThresholdIncludesStrategic,
            competition.DivergenceThresholdPercent);
}
