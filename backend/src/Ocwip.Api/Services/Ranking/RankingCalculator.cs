using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Ranking;

/// <summary>One application with everything the ranking reads about it.</summary>
/// <param name="MeritCards">The scores of its FINISHED merit cards only.</param>
/// <param name="MeritScale">
/// The highest merit score the card allows (50 in 2026), for the divergence
/// warning; null when the card does not say.
/// </param>
internal sealed record RankingInput(
    Guid ApplicationId,
    string? Number,
    string EntityName,
    EntityType EntityType,
    string? ProjectTitle,
    decimal? RequestedGrant,
    DateTimeOffset? SubmittedAt,
    FormalStanding Formal,
    IReadOnlyList<EvaluationScores> MeritCards,
    decimal? MeritScale);

/// <summary>
/// The ranking rule (T-39) with no database in sight, so every clause of the
/// regulations has a test of its own: combine the finished merit cards (sum
/// or average), rank only what passed formally and has every card, order by
/// the total, and break a tie by the earlier submission (regulamin 2026).
/// </summary>
internal static class RankingCalculator
{
    public static IReadOnlyList<RankingRow> Rank(IEnumerable<RankingInput> inputs, Competition settings)
    {
        var rows = inputs.Select(input => Row(input, settings)).ToList();

        var ranked = rows
            .Where(row => row.Qualifies)
            .OrderByDescending(row => row.Row.TotalScore)
            .ThenBy(row => row.Row.SubmittedAt)
            .Select((row, index) => row.Row with { Rank = index + 1 });

        // The rest after the list, in the order of their numbers, the order
        // the operator's list of applications uses (T-35).
        var unranked = rows
            .Where(row => !row.Qualifies)
            .OrderBy(row => row.Row.Number?.Length ?? int.MaxValue)
            .ThenBy(row => row.Row.Number)
            .Select(row => row.Row);

        return ranked.Concat(unranked).ToList();
    }

    private static (RankingRow Row, bool Qualifies) Row(RankingInput input, Competition settings)
    {
        var cards = input.MeritCards;
        var merit = Combine(cards.Select(card => card.MeritScore ?? 0m), settings.ScoreAggregation);
        var strategic = Combine(cards.Select(card => card.StrategicScore ?? 0m), settings.ScoreAggregation);
        var total = merit is null ? null : merit + (strategic ?? 0m);

        bool? passes = settings.MeritThreshold is { } threshold && merit is not null
            ? merit + (settings.ThresholdIncludesStrategic ? strategic ?? 0m : 0m) >= threshold
            : null;

        var recommendations = cards.Where(card => card.RecommendedGrant is not null).ToList();
        decimal? recommended = recommendations.Count == 0
            ? null
            : Math.Round(recommendations.Average(card => card.RecommendedGrant!.Value), 2, MidpointRounding.AwayFromZero);

        var qualifies = input.Formal == FormalStanding.Passed
            && cards.Count >= settings.EvaluatorsPerApplication;

        var row = new RankingRow(
            Rank: null,
            input.ApplicationId,
            input.Number,
            input.EntityName,
            input.EntityType,
            input.ProjectTitle,
            input.RequestedGrant,
            input.SubmittedAt,
            input.Formal,
            cards.Count,
            settings.EvaluatorsPerApplication,
            merit,
            strategic,
            total,
            passes,
            Diverges(cards, input.MeritScale, settings.DivergenceThresholdPercent),
            recommended);

        return (row, qualifies);
    }

    private static decimal? Combine(IEnumerable<decimal> scores, ScoreAggregation aggregation)
    {
        var list = scores.ToList();

        if (list.Count == 0)
        {
            return null;
        }

        return aggregation == ScoreAggregation.Average
            ? Math.Round(list.Average(), 2, MidpointRounding.AwayFromZero)
            : list.Sum();
    }

    /// <summary>
    /// The report's rule (decision 12): cards further apart than the
    /// threshold, as a percentage of the merit scale, are flagged. The system
    /// flags and never resolves; a third opinion or an average is the
    /// operator's call, with a reason.
    /// </summary>
    private static bool Diverges(IReadOnlyList<EvaluationScores> cards, decimal? scale, decimal? thresholdPercent)
    {
        if (thresholdPercent is not { } threshold || scale is not { } max || max <= 0m || cards.Count < 2)
        {
            return false;
        }

        var scores = cards.Select(card => card.MeritScore ?? 0m).ToList();
        return (scores.Max() - scores.Min()) / max * 100m > threshold;
    }

    /// <summary>
    /// The highest merit score a card allows: the sum of the maxValue of every
    /// field its merit sum adds up. Null when a part has no ceiling, because a
    /// guessed scale would warn about the wrong difference.
    /// </summary>
    public static decimal? MeritScale(FormDocument card)
    {
        var fields = card.Sections.SelectMany(section => section.Fields).ToDictionary(field => field.Key);
        var sum = fields.Values.FirstOrDefault(field => field.Role == FormFieldRole.MeritScore);

        if (sum?.Calculation is null)
        {
            return null;
        }

        decimal total = 0m;

        foreach (var operand in sum.Calculation.Operands)
        {
            if (!fields.TryGetValue(operand, out var part) || part.MaxValue is not { } max)
            {
                return null;
            }

            total += max;
        }

        return total;
    }
}
