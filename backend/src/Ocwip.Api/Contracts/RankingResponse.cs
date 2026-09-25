using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>The evaluation settings of one competition (T-39, report step 5.0).</summary>
public sealed record EvaluationSettingsRequest(
    int EvaluatorsPerApplication,
    ScoreAggregation ScoreAggregation,
    decimal? MeritThreshold,
    bool ThresholdIncludesStrategic,
    decimal? DivergenceThresholdPercent);

public sealed record EvaluationSettingsResponse(
    int EvaluatorsPerApplication,
    ScoreAggregation ScoreAggregation,
    decimal? MeritThreshold,
    bool ThresholdIncludesStrategic,
    decimal? DivergenceThresholdPercent);

/// <summary>Where the formal evaluation of one application stands.</summary>
public enum FormalStanding
{
    NotStarted,
    InProgress,
    Passed,
    Failed,
}

/// <summary>
/// The ranking list of a competition (T-39): every submitted application, the
/// ranked ones first in order, then the ones that do not qualify yet. Nothing
/// here grants anything; the decision is the operator's (T-42).
/// </summary>
public sealed record RankingResponse(
    Guid CompetitionId,
    EvaluationSettingsResponse Settings,
    IReadOnlyList<RankingRow> Rows);

/// <param name="Rank">
/// Place on the list, only for an application with a positive formal
/// evaluation and every merit card it needs finished; null otherwise.
/// </param>
/// <param name="MeritScore">Combined per the settings from the finished merit cards, null before the first.</param>
/// <param name="PassesThreshold">Null while there is no threshold or no finished merit card.</param>
/// <param name="Diverges">
/// True when two finished cards differ by more than the divergence threshold
/// of the merit scale: a warning for the operator, never a decision.
/// </param>
/// <param name="RecommendedGrant">The average of the experts' recommendations, null before any.</param>
public sealed record RankingRow(
    int? Rank,
    Guid ApplicationId,
    string? Number,
    string EntityName,
    EntityType EntityType,
    string? ProjectTitle,
    decimal? RequestedGrant,
    DateTimeOffset? SubmittedAt,
    FormalStanding Formal,
    int MeritCardsFinished,
    int MeritCardsRequired,
    decimal? MeritScore,
    decimal? StrategicScore,
    decimal? TotalScore,
    bool? PassesThreshold,
    bool Diverges,
    decimal? RecommendedGrant);
