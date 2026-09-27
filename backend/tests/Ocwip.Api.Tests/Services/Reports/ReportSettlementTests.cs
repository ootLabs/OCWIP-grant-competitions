using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Reports;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

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
        Assert.Equal(new StoredCostReview(1, 50m, 20m, "Brak faktury."), Assert.Single(review!));
        Assert.Equal(review, ReportSettlement.Read(ReportSettlement.Write(review!)));
    }
}
