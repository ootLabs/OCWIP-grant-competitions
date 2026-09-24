using System.Text.Json.Nodes;
using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.AnswerSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The four budget rules of T-31, on the three cost tables of the 2026
/// application: the grant under the competition's ceiling, table B and table
/// C each under a threshold the competition sets, and every row's value equal
/// to units times price. The report's own example is the one tested first: a
/// limit of nine thousand, and somebody enters ten.
/// </summary>
public sealed class BudgetLimitsTests
{
    private const char Nbsp = '\u00A0';

    /// <summary>The 2026 templates: 9000 per application, B at 50%, C at 10%.</summary>
    private static readonly IReadOnlyDictionary<string, decimal?> Settings2026 =
        new Dictionary<string, decimal?>
        {
            ["competition.maxGrantAmount"] = 9000m,
            ["competition.maxInstitutionalDevelopmentPercent"] = 50m,
            ["competition.maxIndirectCostPercent"] = 10m,
        };

    [Fact]
    public void The_definition_with_thresholds_from_the_competition_passes_the_contract()
    {
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.BudgetWithThreeTables());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Ten_thousand_against_a_limit_of_nine_names_the_excess_and_the_ceiling()
    {
        var answers = Budget(a: [Row(10, 1000)]);

        var result = Submit(answers);

        Assert.Equal(
            $"Przekroczono dopuszczalną wartość o 1000,00{Nbsp}zł. Maksymalnie 9000,00{Nbsp}zł.",
            MessageFor(result, "dotacja"));
    }

    [Theory]
    [InlineData(9000, true)]
    [InlineData(8999, true)]
    [InlineData(9001, false)]
    public void The_grant_ceiling_holds_to_the_zloty(int costs, bool passes)
    {
        var result = Submit(Budget(a: [Row(1, costs)]));

        Assert.Equal(passes, MessageFor(result, "dotacja") is null);
    }

    [Fact]
    public void Indirect_costs_over_their_threshold_name_the_table_and_the_position()
    {
        // A grant of 9000, so 10% leaves 900 for table C; the third row is
        // where the running total goes past it.
        var answers = Budget(
            a: [Row(1, 8000)],
            c: [Row(1, 500), Row(1, 300), Row(1, 200)]);

        var result = Submit(answers);

        Assert.Equal(
            $"Tabela „Koszty pośrednie”: przekroczono dopuszczalną wartość o 100,00{Nbsp}zł. "
            + $"Maksymalnie 900,00{Nbsp}zł.",
            MessageFor(result, "suma_c"));
        Assert.Equal(
            $"Od tej pozycji suma tabeli „Koszty pośrednie” przekracza dopuszczalną wartość "
            + $"o 100,00{Nbsp}zł. Maksymalnie 900,00{Nbsp}zł.",
            MessageFor(result, "budzet_c[2].wartosc"));
        Assert.Null(MessageFor(result, "budzet_c[1].wartosc"));
    }

    [Theory]
    [InlineData(4500, true)]
    [InlineData(4499, true)]
    [InlineData(4501, false)]
    public void Institutional_development_holds_to_its_threshold_to_the_zloty(int b, bool passes)
    {
        // The grant stays 9000 whatever table B holds: the rest is table A.
        var result = Submit(Budget(a: [Row(1, 9000 - b)], b: [Row(1, b)]));

        Assert.Equal(passes, MessageFor(result, "suma_b") is null);
    }

    [Theory]
    [InlineData(900, true)]
    [InlineData(899, true)]
    [InlineData(901, false)]
    public void Indirect_costs_hold_to_their_threshold_to_the_zloty(int c, bool passes)
    {
        var result = Submit(Budget(a: [Row(1, 9000 - c)], c: [Row(1, c)]));

        Assert.Equal(passes, MessageFor(result, "suma_c") is null);
    }

    [Fact]
    public void A_threshold_the_competition_left_empty_is_not_checked()
    {
        // The category has no threshold in this competition.
        var settings = new Dictionary<string, decimal?>(Settings2026)
        {
            ["competition.maxInstitutionalDevelopmentPercent"] = null,
        };

        var result = Validate(
            FormDefinitionSamples.BudgetWithThreeTables(),
            Budget(a: [Row(1, 1000)], b: [Row(1, 8000)]),
            AnswerStrictness.Submission,
            settings);

        Assert.Null(MessageFor(result, "suma_b"));
    }

    [Fact]
    public void A_row_value_is_units_times_price_and_a_request_cannot_carry_its_own()
    {
        // Five hours at 120,50: the grant is 602,50 because the row is.
        var answers = Budget(a: [Row(5, 120.5m)]);
        ((JsonObject)((JsonArray)answers["budzet_a"]!)[0]!)["wartosc"] = 1m;

        var result = Validate(
            FormDefinitionSamples.BudgetWithThreeTables(),
            answers,
            AnswerStrictness.Draft,
            Settings2026);

        Assert.Equal(
            "To pole jest wyliczane i nie przyjmuje odpowiedzi.",
            MessageFor(result, "budzet_a[0].wartosc"));
    }

    [Fact]
    public void The_grant_is_computed_from_the_rows_at_full_precision()
    {
        // Three rows of 3000,003 come to 9000,009: over the ceiling. Rounded
        // to grosze row by row they would come to exactly 9000,00 and pass
        // (D13: full precision all the way, rounding only in the message).
        var answers = Budget(a: [Row(1, 3000.003m), Row(1, 3000.003m), Row(1, 3000.003m)]);

        var result = Submit(answers);

        Assert.Equal(
            $"Przekroczono dopuszczalną wartość o 0,01{Nbsp}zł. Maksymalnie 9000,00{Nbsp}zł.",
            MessageFor(result, "dotacja"));
    }

    private static AnswerValidationResult Submit(JsonObject answers) =>
        Validate(
            FormDefinitionSamples.BudgetWithThreeTables(),
            answers,
            AnswerStrictness.Submission,
            Settings2026);

    private static JsonObject Budget(
        JsonObject[] a,
        JsonObject[]? b = null,
        JsonObject[]? c = null,
        decimal contribution = 0m) =>
        new()
        {
            ["budzet_a"] = new JsonArray(a.Select(row => (JsonNode?)row).ToArray()),
            ["budzet_b"] = new JsonArray((b ?? []).Select(row => (JsonNode?)row).ToArray()),
            ["budzet_c"] = new JsonArray((c ?? []).Select(row => (JsonNode?)row).ToArray()),
            ["wklad_wlasny"] = contribution,
        };

    private static JsonObject Row(decimal units, decimal price) =>
        new() { ["nazwa"] = "Pozycja", ["liczba"] = units, ["cena"] = price };
}
