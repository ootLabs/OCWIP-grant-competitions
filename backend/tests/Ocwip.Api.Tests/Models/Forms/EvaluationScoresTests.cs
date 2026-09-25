using System.Text.Json.Nodes;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The result of one card, read from its answers (T-38): a criterion that is
/// not asked of this applicant neither fails nor scores.
/// </summary>
public sealed class EvaluationScoresTests
{
    private static FormDocument Formal() =>
        FormSchemaValidator.Validate(EvaluationCardSamples.FormalCard(), FormPurpose.FormalEvaluation).Document!;

    private static FormDocument Merit() =>
        FormSchemaValidator.Validate(EvaluationCardSamples.MeritCard(), FormPurpose.MeritEvaluation).Document!;

    [Fact]
    public void A_formal_card_passes_when_every_criterion_asked_is_met()
    {
        var answers = new JsonObject { ["w_terminie"] = true, ["przychod"] = true };

        var scores = EvaluationScores.Read(Formal(), AnswerSamples.Element(answers), EntityType.Organisation);

        Assert.True(scores.FormalPassed);
    }

    [Fact]
    public void A_criterion_not_asked_of_this_applicant_does_not_fail_the_card()
    {
        // The patron criterion is answered "no", but an organisation has no
        // patron: the answer is not part of this card.
        var answers = new JsonObject
        {
            ["w_terminie"] = true,
            ["przychod"] = true,
            ["bez_funkcji_u_patrona"] = false,
        };

        var scores = EvaluationScores.Read(Formal(), AnswerSamples.Element(answers), EntityType.Organisation);

        Assert.True(scores.FormalPassed);
    }

    [Fact]
    public void One_unmet_criterion_fails_the_card_and_an_unanswered_one_leaves_it_open()
    {
        var failed = new JsonObject { ["w_terminie"] = false, ["przychod"] = true };
        var open = new JsonObject { ["w_terminie"] = true };

        Assert.False(EvaluationScores.Read(Formal(), AnswerSamples.Element(failed), EntityType.Organisation).FormalPassed);
        Assert.Null(EvaluationScores.Read(Formal(), AnswerSamples.Element(open), EntityType.Organisation).FormalPassed);
    }

    [Fact]
    public void Merit_and_strategic_points_are_summed_apart_and_a_criterion_not_asked_scores_nothing()
    {
        // "z_patronem" is asked of informal groups only, so for an
        // organisation its "yes" is worth nothing.
        var answers = new JsonObject
        {
            ["pomysl"] = 17,
            ["budzet"] = 3,
            ["biale_plamy"] = true,
            ["z_patronem"] = true,
            ["kwota"] = 6500,
        };

        var organisation = EvaluationScores.Read(Merit(), AnswerSamples.Element(answers), EntityType.Organisation);
        var group = EvaluationScores.Read(Merit(), AnswerSamples.Element(answers), EntityType.InformalGroup);

        Assert.Equal(20m, organisation.MeritScore);
        Assert.Equal(1m, organisation.StrategicScore);
        Assert.Equal(2m, group.StrategicScore);
        Assert.Equal(6500m, organisation.RecommendedGrant);
        Assert.Null(organisation.FormalPassed);
    }

    [Fact]
    public void An_empty_recommended_grant_is_no_recommendation_rather_than_zero()
    {
        var scores = EvaluationScores.Read(Merit(), AnswerSamples.Element(new JsonObject()), EntityType.Organisation);

        Assert.Null(scores.RecommendedGrant);
        Assert.Equal(0m, scores.MeritScore);
    }

    [Fact]
    public void A_required_criterion_not_asked_of_this_applicant_is_not_missing()
    {
        var answers = AnswerSamples.Element(new JsonObject { ["w_terminie"] = true, ["przychod"] = true });

        var organisation = AnswerValidator.Validate(
            Formal(), answers, new Dictionary<string, decimal?>(), AnswerStrictness.Submission, EntityType.Organisation);
        var patronGroup = AnswerValidator.Validate(
            Formal(), answers, new Dictionary<string, decimal?>(), AnswerStrictness.Submission, EntityType.PatronInformalGroup);

        Assert.Empty(organisation.Errors);
        Assert.Contains(patronGroup.Errors, error => error.Key == "bez_funkcji_u_patrona");
    }
}
