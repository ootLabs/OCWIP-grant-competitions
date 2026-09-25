using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Ranking;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Services.Ranking;

/// <summary>
/// The ranking rule of T-39, clause by clause, from the 2026 regulations:
/// two experts, the sum of their cards, a threshold of 50 without the
/// strategic points, and a tie going to the earlier submission.
/// </summary>
public sealed class RankingCalculatorTests
{
    private static readonly DateTimeOffset Morning = new(2026, 4, 10, 8, 0, 0, TimeSpan.Zero);

    private static Competition Rules(
        ScoreAggregation aggregation = ScoreAggregation.Sum,
        decimal? threshold = 50m,
        bool includesStrategic = false,
        decimal? divergence = null) =>
        new()
        {
            EvaluatorsPerApplication = 2,
            ScoreAggregation = aggregation,
            MeritThreshold = threshold,
            ThresholdIncludesStrategic = includesStrategic,
            DivergenceThresholdPercent = divergence,
        };

    private static EvaluationScores Card(decimal merit, decimal strategic = 0m, decimal? grant = null) =>
        new(null, merit, strategic, grant);

    private static RankingInput Input(
        string number,
        int minutesAfterMorning,
        FormalStanding formal,
        params EvaluationScores[] cards) =>
        new(
            Guid.NewGuid(),
            number,
            $"Podmiot {number}",
            EntityType.Organisation,
            $"Projekt {number}",
            7000m,
            Morning.AddMinutes(minutesAfterMorning),
            formal,
            cards,
            MeritScale: 50m);

    [Fact]
    public void Applications_are_ordered_by_the_sum_of_both_cards_and_a_tie_goes_to_the_earlier_submission()
    {
        var rows = RankingCalculator.Rank(
            [
                Input("001", 30, FormalStanding.Passed, Card(40), Card(40)),
                Input("002", 10, FormalStanding.Passed, Card(45), Card(45)),
                // Same 80 as 001 but submitted earlier: it goes first.
                Input("003", 20, FormalStanding.Passed, Card(35), Card(45)),
            ],
            Rules());

        Assert.Equal(["002", "003", "001"], rows.Select(row => row.Number));
        Assert.Equal([1, 2, 3], rows.Select(row => row.Rank));
        Assert.Equal(90m, rows[0].TotalScore);
    }

    [Fact]
    public void Strategic_points_add_to_the_total_but_not_to_the_2026_threshold()
    {
        var rows = RankingCalculator.Rank(
            [Input("001", 0, FormalStanding.Passed, Card(24, strategic: 1), Card(24, strategic: 1))],
            Rules());

        // 48 merit points and 2 strategic: 50 in total, 48 against a threshold of 50.
        Assert.Equal(50m, rows[0].TotalScore);
        Assert.False(rows[0].PassesThreshold);

        var withStrategic = RankingCalculator.Rank(
            [Input("001", 0, FormalStanding.Passed, Card(24, strategic: 1), Card(24, strategic: 1))],
            Rules(includesStrategic: true));

        Assert.True(withStrategic[0].PassesThreshold);
    }

    [Fact]
    public void The_average_is_there_for_a_competition_that_chooses_it()
    {
        var rows = RankingCalculator.Rank(
            [Input("001", 0, FormalStanding.Passed, Card(41), Card(44, grant: 6000m))],
            Rules(ScoreAggregation.Average, threshold: 40m));

        Assert.Equal(42.5m, rows[0].MeritScore);
        Assert.True(rows[0].PassesThreshold);
        Assert.Equal(6000m, rows[0].RecommendedGrant);
    }

    [Fact]
    public void Only_a_formally_passed_application_with_every_card_finished_gets_a_place()
    {
        var rows = RankingCalculator.Rank(
            [
                Input("004", 0, FormalStanding.Failed, Card(50), Card(50)),
                Input("002", 0, FormalStanding.Passed, Card(50)),
                Input("003", 0, FormalStanding.InProgress),
                Input("001", 0, FormalStanding.Passed, Card(20), Card(20)),
            ],
            Rules());

        Assert.Equal(1, rows[0].Rank);
        Assert.Equal("001", rows[0].Number);
        // The rest after the list, by number, with the progress still shown.
        Assert.Equal(["002", "003", "004"], rows.Skip(1).Select(row => row.Number));
        Assert.All(rows.Skip(1), row => Assert.Null(row.Rank));
        Assert.Equal(1, rows[1].MeritCardsFinished);
        Assert.Equal(2, rows[1].MeritCardsRequired);
        Assert.Null(rows[2].MeritScore);
    }

    [Theory]
    [InlineData(45, 20, true)]
    [InlineData(40, 35, false)]
    public void Cards_further_apart_than_the_threshold_are_flagged_and_nothing_more(
        int first, int second, bool diverges)
    {
        var rows = RankingCalculator.Rank(
            [Input("001", 0, FormalStanding.Passed, Card(first), Card(second))],
            Rules(divergence: 30m));

        Assert.Equal(diverges, rows[0].Diverges);
        // Flagged or not, the application keeps its place: the operator decides.
        Assert.Equal(1, rows[0].Rank);
    }

    [Fact]
    public void The_merit_scale_is_read_from_the_card()
    {
        var card = FormSchemaValidator.Validate(
            EvaluationCardSamples.MeritCard(), FormPurpose.MeritEvaluation).Document!;

        // pomysl 0-20 plus budzet 0-4.
        Assert.Equal(24m, RankingCalculator.MeritScale(card));
    }
}
