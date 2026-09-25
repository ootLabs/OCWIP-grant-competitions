using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services.Ranking;

internal enum RankingOutcome
{
    Succeeded,
    CompetitionNotFound,
    Inactive,
    InvalidSettings,
}

internal sealed record RankingResult(
    RankingOutcome Outcome,
    RankingResponse? Ranking = null,
    EvaluationSettingsResponse? Settings = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>The evaluation settings and the ranking list of a competition (T-39), operator only.</summary>
internal interface IRankingService
{
    Task<RankingResult> GetSettingsAsync(Guid competitionId, CancellationToken cancellationToken);

    Task<RankingResult> UpdateSettingsAsync(
        Guid competitionId, EvaluationSettingsRequest request, CancellationToken cancellationToken);

    Task<RankingResult> GetRankingAsync(Guid competitionId, CancellationToken cancellationToken);
}
