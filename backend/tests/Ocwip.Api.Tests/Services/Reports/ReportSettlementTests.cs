using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Reports;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Services.Reports;

/// <summary>T-50b: what the grant spending adds up to and what goes back.</summary>
public sealed class ReportSettlementTests
{
    private static readonly FormDocument Form =
        FormSchemaValidator.Validate(ReportFormSamples.SettledReport(), FormPurpose.Report).Document!;

    private static JsonElement Answers(params decimal[] spent) =>
        JsonSerializer.SerializeToElement(new { budzet = spent.Select(value => new { wykonana = value }) });

    private static ReportSettlementResponse Settle(JsonElement answers, decimal? grant, params StoredCostReview[] review) =>
        ReportSettlement.Compute(Form, answers, EntityType.Organisation, grant, review)!;

    [Fact]
    public void Nothing_refused_and_everything_spent_returns_nothing()
    {
        var settlement = Settle(Answers(1000m, 600m), 1600m);

        Assert.Equal(1600m, settlement.Accepted);
        Assert.Equal(0m, settlement.Refund);
    }

    [Fact]
    public void The_refund_is_the_grant_less_what_was_accepted()
    {
        var settlement = Settle(Answers(1000m, 500m), 1600m, new StoredCostReview(0, 1000m, 200m, "Ponad plan."));

        Assert.Equal(1500m, settlement.GrantSpent);
        Assert.Equal(200m, settlement.Refused);
        Assert.Equal(1300m, settlement.Accepted);
        Assert.Equal(300m, settlement.Refund);
        Assert.Equal("Ponad plan.", settlement.Rows[0].Reason);
    }

    [Fact]
    public void Spending_above_the_grant_never_makes_a_negative_refund()
    {
        Assert.Equal(0m, Settle(Answers(2000m), 1600m).Refund);
    }

    [Fact]
    public void Without_a_grant_there_is_no_refund_to_count()
    {
        Assert.Null(Settle(Answers(100m), null).Refund);
    }

    [Fact]
    public void A_judgement_on_a_row_that_changed_stops_counting()
    {
        var settlement = Settle(Answers(900m), 1600m, new StoredCostReview(0, 1000m, 200m, "Ponad plan."));

        Assert.Equal(0m, settlement.Refused);
        Assert.Null(settlement.Rows[0].Reason);
    }

    [Fact]
    public void A_judgement_on_a_row_that_is_gone_stops_counting()
    {
        var kept = ReportSettlement.Keep(
            Form, Answers(1000m), EntityType.Organisation,
            [new StoredCostReview(0, 1000m, 100m, "A."), new StoredCostReview(1, 500m, 100m, "B.")]);

        Assert.Equal(0, Assert.Single(kept).Row);
    }

    [Fact]
    public void A_report_without_a_budget_has_no_settlement()
    {
        var plain = FormSchemaValidator.Validate(ReportFormSamples.Report(), FormPurpose.Report).Document!;

        Assert.Null(ReportSettlement.Compute(plain, Answers(100m), EntityType.Organisation, 1600m, []));
    }

    [Fact]
    public void The_review_refuses_more_than_was_spent_and_a_fraction_of_a_grosz()
    {
        var (_, errors) = ReportSettlement.Check(
            Form, Answers(100m, 100m), EntityType.Organisation,
            [new CostReviewItem(0, 100.01m, "A."), new CostReviewItem(1, 0.001m, "B.")]);

        Assert.Equal(2, errors!.Count);
        Assert.Contains("(100,00 zł)", errors["items[0]"].Single());
    }

    [Fact]
    public void The_review_refuses_the_same_row_twice()
    {
        var (_, errors) = ReportSettlement.Check(
            Form, Answers(100m), EntityType.Organisation,
            [new CostReviewItem(0, 10m, "A."), new CostReviewItem(0, 20m, "B.")]);

        Assert.Contains("dwa razy", Assert.Single(errors!).Value.Single());
    }

    [Fact]
    public void The_review_stores_the_spending_it_judged()
    {
        var (review, errors) = ReportSettlement.Check(
            Form, Answers(100m, 50m), EntityType.Organisation, [new CostReviewItem(1, 20m, "  Brak faktury. ")]);

        Assert.Null(errors);
        Assert.Equal(new StoredCostReview(1, 50m, 20m, "Brak faktury.", "budzet"), Assert.Single(review!));
        Assert.Equal(review, ReportSettlement.Read(ReportSettlement.Write(review!)));
    }

    /// <summary>T-95: a budget split into tables, one per part, like A and B of the 2026 report.</summary>
    private static readonly FormDocument TwoBudgets = FormSchemaValidator.Validate(
        WithFields(Budget("czesc_a"), Budget("czesc_b")), FormPurpose.Report).Document!;

    private static string Budget(string key) =>
        Field(key, "repeatableTable", $$"""
            "role": "reportBudget",
            "table": { "minRows": 1, "columns": [ {{Field("wydatek", "amount", "\"minValue\": 0, \"role\": \"grantSpent\"")}} ] }
            """);

    private static readonly JsonElement TwoBudgetAnswers = JsonSerializer.SerializeToElement(new
    {
        czesc_a = new[] { new { wydatek = 1000m }, new { wydatek = 300m } },
        czesc_b = new[] { new { wydatek = 200m } },
    });

    [Fact]
    public void Every_budget_table_counts_and_each_row_names_its_table()
    {
        var settlement = ReportSettlement.Compute(TwoBudgets, TwoBudgetAnswers, EntityType.Organisation, 1600m,
            [new StoredCostReview(0, 200m, 50m, "Bez faktury.", "czesc_b")])!;

        Assert.Equal(1500m, settlement.GrantSpent);
        Assert.Equal(1450m, settlement.Accepted);
        Assert.Equal(150m, settlement.Refund);
        Assert.Equal(
            [("czesc_a", 0, 0m), ("czesc_a", 1, 0m), ("czesc_b", 0, 50m)],
            settlement.Rows.Select(row => (row.Budget, row.Row, row.Refused)));
    }

    [Fact]
    public void The_same_row_of_two_tables_is_judged_apart_and_an_unknown_table_is_refused()
    {
        var (review, errors) = ReportSettlement.Check(TwoBudgets, TwoBudgetAnswers, EntityType.Organisation,
            [new CostReviewItem(0, 100m, "A.", "czesc_b"), new CostReviewItem(0, 100m, "B.", "czesc_a")]);

        Assert.Null(errors);
        Assert.Equal(["czesc_a", "czesc_b"], review!.Select(x => x.Budget));

        var (_, unknown) = ReportSettlement.Check(TwoBudgets, TwoBudgetAnswers, EntityType.Organisation,
            [new CostReviewItem(0, 10m, "A.", "czesc_z"), new CostReviewItem(3, 10m, "B.", "czesc_b")]);

        Assert.Contains("czesc_z", unknown!["items[0]"].Single());
        Assert.Contains("pozycji 4 w tabeli", unknown["items[1]"].Single());
    }

    [Fact]
    public void A_review_stored_without_a_table_is_about_the_first_one()
    {
        var settlement = ReportSettlement.Compute(TwoBudgets, TwoBudgetAnswers, EntityType.Organisation, 1600m,
            [new StoredCostReview(1, 300m, 100m, "Stary zapis.")])!;

        Assert.Equal(100m, settlement.Rows.Single(row => row.Budget == "czesc_a" && row.Row == 1).Refused);
    }
}
