using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the contract accepts on an evaluation card and refuses on the wrong
/// kind of document (T-38): appliesTo, points, the evaluation roles and the
/// one role each card cannot do without.
/// </summary>
public sealed class EvaluationCardContractTests
{
    [Fact]
    public void A_formal_card_is_read_with_its_criteria_and_who_they_are_asked_of()
    {
        var result = FormSchemaValidator.Validate(
            EvaluationCardSamples.FormalCard(), FormPurpose.FormalEvaluation);

        Assert.Empty(result.Errors);
        var fields = result.Document!.Sections[0].Fields;
        Assert.Equal(3, fields.Count(field => field.Role == FormFieldRole.FormalCriterion));
        Assert.Null(fields[0].AppliesTo);
        Assert.Equal([EntityType.Organisation], fields[2].AppliesTo);
    }

    [Fact]
    public void A_merit_card_sums_scored_yes_or_no_fields()
    {
        var result = FormSchemaValidator.Validate(
            EvaluationCardSamples.MeritCard(), FormPurpose.MeritEvaluation);

        Assert.Empty(result.Errors);
        Assert.Equal(1m, result.Document!.Sections[0].Fields.Single(field => field.Key == "biale_plamy").Points);
    }

    [Fact]
    public void A_formal_card_without_a_criterion_is_refused()
    {
        var result = FormSchemaValidator.Validate(
            WithFields(Field("w_terminie", "yesNo")), FormPurpose.FormalEvaluation);

        Assert.Contains("nie ma żadnego kryterium", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_merit_card_without_a_merit_sum_is_refused()
    {
        var result = FormSchemaValidator.Validate(
            WithFields(Field("pomysl", "number")), FormPurpose.MeritEvaluation);

        Assert.Contains("nie ma sumy punktów", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("\"appliesTo\": [\"Organisation\"]", "tylko na karcie oceny")]
    [InlineData("\"points\": 1", "tylko na karcie oceny")]
    [InlineData("\"role\": \"formalCriterion\"", "należy do karty oceny")]
    public void An_application_form_may_not_carry_what_only_a_card_uses(string extra, string message)
    {
        var result = FormSchemaValidator.Validate(WithFields(Field("pole", "yesNo", extra)));

        Assert.Contains(message, Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_card_may_not_carry_an_application_role()
    {
        var result = FormSchemaValidator.Validate(
            WithFields(
                Field("kryterium", "yesNo", "\"role\": \"formalCriterion\""),
                Field("koszt", "amount", "\"role\": \"totalCost\"")),
            FormPurpose.FormalEvaluation);

        Assert.Contains("należy do formularza wniosku", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void A_merit_card_may_not_carry_a_formal_criterion()
    {
        var definition = WithFields(
            Field("pomysl", "number"),
            Field(
                "suma",
                "calculated",
                "\"calculation\": { \"kind\": \"sum\", \"operands\": [\"pomysl\"] }, \"role\": \"meritScore\""),
            Field("kryterium", "yesNo", "\"role\": \"formalCriterion\""));

        var result = FormSchemaValidator.Validate(definition, FormPurpose.MeritEvaluation);

        Assert.Contains("należy do drugiej karty oceny", Assert.Single(result.Errors).Message);
    }

    [Theory]
    // Spelled as EntityType on the wire, never case-insensitively (R-34).
    [InlineData("[\"organisation\"]", "nie ma rodzaju wnioskodawcy")]
    [InlineData("[\"Stowarzyszenie\"]", "nie ma rodzaju wnioskodawcy")]
    [InlineData("[]", "pusta lista")]
    [InlineData("[\"Organisation\", \"Organisation\"]", "powtarza się")]
    public void A_list_of_applicant_kinds_names_real_kinds_once_each(string list, string message)
    {
        var definition = WithFields(
            Field("kryterium", "yesNo", $"\"role\": \"formalCriterion\", \"appliesTo\": {list}"));

        var result = FormSchemaValidator.Validate(definition, FormPurpose.FormalEvaluation);

        Assert.Contains(message, Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("number", "\"points\": 1", "tylko pole tak albo nie")]
    [InlineData("yesNo", "\"points\": 0", "większe od zera")]
    public void Points_go_on_a_yes_or_no_field_and_are_positive(string type, string extra, string message)
    {
        var definition = WithFields(
            Field("kryterium", "yesNo", "\"role\": \"formalCriterion\""),
            Field("pole", type, extra));

        var result = FormSchemaValidator.Validate(definition, FormPurpose.FormalEvaluation);

        Assert.Contains(message, Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("\"points\": 1", "product")]
    [InlineData("", "sum")]
    public void A_yes_or_no_counts_only_when_scored_and_only_in_a_sum(string points, string kind)
    {
        var definition = WithFields(
            Field("pomysl", "number"),
            Field("plamy", "yesNo", points),
            Field(
                "wynik",
                "calculated",
                $"\"calculation\": {{ \"kind\": \"{kind}\", \"operands\": [\"pomysl\", \"plamy\"] }}"),
            Field(
                "suma",
                "calculated",
                "\"calculation\": { \"kind\": \"sum\", \"operands\": [\"pomysl\"] }, \"role\": \"meritScore\""));

        var result = FormSchemaValidator.Validate(definition, FormPurpose.MeritEvaluation);

        Assert.Contains("nie jest liczbą", Assert.Single(result.Errors).Message);
    }
}
