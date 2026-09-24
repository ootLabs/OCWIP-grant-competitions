using System.Text.Json.Nodes;
using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.AnswerSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// Limits against the budget sample (D11, D12): the grant is what is left
/// after the applicant's own contribution, and a message about it names the
/// ceiling for this application, not the rule.
/// </summary>
public sealed class AnswerLimitsTests
{
    /// <summary>The report's own example: a limit of nine thousand.</summary>
    private static readonly IReadOnlyDictionary<string, decimal?> NineThousand =
        new Dictionary<string, decimal?> { ["competition.maxGrantAmount"] = 9000m };

    private const char Nbsp = '\u00A0';

    [Fact]
    public void Over_the_grant_ceiling_the_message_says_by_how_much_and_what_is_allowed()
    {
        // 2 x 5000 = 10 000 of costs, nothing of their own: a grant of 10 000.
        var result = Validate(
            FormDefinitionSamples.Budget(),
            Budget(units: 2, price: 5000, contribution: 0),
            AnswerStrictness.Submission,
            NineThousand);

        Assert.Equal(
            $"Przekroczono dopuszczalną wartość o 1000,00{Nbsp}zł. "
            + $"Maksymalnie 9000,00{Nbsp}zł.",
            MessageFor(result, "dotacja"));
    }

    [Theory]
    [InlineData(1000, true)]  // exactly on the limit
    [InlineData(1001, true)]  // a zloty under it
    [InlineData(999, false)]  // a zloty over it
    public void The_limit_is_measured_on_the_computed_grant_to_the_zloty(
        int contribution, bool passes)
    {
        var result = Validate(
            FormDefinitionSamples.Budget(),
            Budget(units: 2, price: 5000, contribution: contribution),
            AnswerStrictness.Submission,
            NineThousand);

        Assert.Equal(passes, MessageFor(result, "dotacja") is null);
    }

    [Fact]
    public void A_percentage_limit_names_the_amount_computed_from_its_basis()
    {
        // A grant of 9000, of which at most 10% may be indirect costs: 900.
        var answers = Budget(units: 2, price: 5000, contribution: 1000);
        answers["budzet_c"] = new JsonArray(
            new JsonObject { ["nazwa"] = "Księgowość", ["cena"] = 950.25m });

        var result = Validate(
            FormDefinitionSamples.Budget(), answers, AnswerStrictness.Submission, NineThousand);

        Assert.Equal(
            $"Tabela „Etykieta budzet_c”: przekroczono dopuszczalną wartość o 50,25{Nbsp}zł. "
            + $"Maksymalnie 900,00{Nbsp}zł.",
            MessageFor(result, "suma_c"));
    }

    [Fact]
    public void A_limit_on_a_setting_the_operator_left_empty_is_not_checked()
    {
        var result = Validate(
            FormDefinitionSamples.Budget(),
            Budget(units: 2, price: 5000, contribution: 0),
            AnswerStrictness.Submission,
            new Dictionary<string, decimal?> { ["competition.maxGrantAmount"] = null });

        Assert.Null(MessageFor(result, "dotacja"));
    }

    [Fact]
    public void A_draft_over_the_limit_still_saves_because_the_budget_is_still_being_typed()
    {
        var result = Validate(
            FormDefinitionSamples.Budget(),
            Budget(units: 2, price: 5000, contribution: 0),
            AnswerStrictness.Draft,
            NineThousand);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Amounts_far_beyond_any_budget_answer_with_a_message_not_an_exception()
    {
        var answers = Budget(units: 0, price: 0, contribution: 0);
        answers["budzet_a"] = new JsonArray(new JsonObject
        {
            ["nazwa"] = "Wszystko",
            ["jednostka"] = "szt.",
            ["liczba"] = 1e20m,
            ["cena"] = 1e20m,
        });

        var result = Validate(
            FormDefinitionSamples.Budget(), answers, AnswerStrictness.Submission, NineThousand);

        Assert.StartsWith("Przekroczono dopuszczalną wartość", MessageFor(result, "dotacja"));
    }

    [Fact]
    public void Money_is_counted_at_full_precision_and_rounded_only_in_the_message()
    {
        // Three rows of 0,333 at 3000 each come to 2997 exactly only when
        // nothing is rounded along the way (D13).
        var answers = Budget(units: 0, price: 0, contribution: 0);
        answers["budzet_a"] = new JsonArray(
            Row(0.333m, 3000m), Row(0.333m, 3000m), Row(0.333m, 3000m));

        var result = Validate(
            FormDefinitionSamples.Budget(),
            answers,
            AnswerStrictness.Submission,
            new Dictionary<string, decimal?> { ["competition.maxGrantAmount"] = 2996.99m });

        Assert.Equal(
            $"Przekroczono dopuszczalną wartość o 0,01{Nbsp}zł. "
            + $"Maksymalnie 2996,99{Nbsp}zł.",
            MessageFor(result, "dotacja"));
    }

    private static JsonObject Budget(decimal units, decimal price, decimal contribution) =>
        new()
        {
            ["budzet_a"] = new JsonArray(Row(units, price)),
            ["wklad_wlasny"] = contribution,
            ["budzet_c"] = new JsonArray(),
        };

    private static JsonObject Row(decimal units, decimal price) =>
        new()
        {
            ["nazwa"] = "Warsztaty",
            ["jednostka"] = "godz.",
            ["liczba"] = units,
            ["cena"] = price,
        };
}
