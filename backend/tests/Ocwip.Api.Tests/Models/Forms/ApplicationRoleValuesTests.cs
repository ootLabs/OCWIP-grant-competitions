using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the operator's list reads out of one application through the role
/// markers of its form (T-35).
/// </summary>
public sealed class ApplicationRoleValuesTests
{
    private static FormDocument Document(params string[] fields) =>
        FormSchemaValidator.Validate(WithFields(fields)).Document!;

    private static readonly string[] Budget =
    [
        Field("tytul", "shortText", "\"maxLength\": 200, \"role\": \"projectTitle\""),
        Field("koszt", "amount", "\"role\": \"totalCost\""),
        Field("wklad", "amount"),
        Field(
            "dotacja",
            "calculated",
            """
            "calculation": { "kind": "difference", "operands": ["koszt", "wklad"] },
            "role": "requestedGrant"
            """),
    ];

    [Fact]
    public void The_grant_is_calculated_the_way_the_form_calculates_it()
    {
        var values = ApplicationRoleValues.Read(
            Document(Budget),
            Parse("""{ "tytul": "  Warsztaty  ", "koszt": 12000.5, "wklad": 2000 }"""));

        Assert.Equal(new ApplicationRoleValues("Warsztaty", 12000.5m, 10000.5m), values);
    }

    [Fact]
    public void A_form_without_roles_leaves_every_value_empty()
    {
        var values = ApplicationRoleValues.Read(
            Document(Field("tytul", "shortText", "\"maxLength\": 200")),
            Parse("""{ "tytul": "Warsztaty" }"""));

        Assert.Equal(new ApplicationRoleValues(null, null, null), values);
    }

    [Fact]
    public void An_empty_typed_amount_is_not_a_zero()
    {
        var values = ApplicationRoleValues.Read(
            Document(Budget),
            Parse("""{ "tytul": "", "wklad": 100 }"""));

        Assert.Null(values.ProjectTitle);
        Assert.Null(values.TotalCost);
        Assert.Equal(-100m, values.RequestedGrant);
    }

    [Fact]
    public void An_answer_hidden_by_a_condition_is_not_read()
    {
        var document = Document(
            Field("ma_tytul", "yesNo"),
            Field(
                "tytul",
                "shortText",
                """
                "maxLength": 200,
                "role": "projectTitle",
                "visibleWhen": { "field": "ma_tytul", "equalsAnyOf": ["true"] }
                """));

        var values = ApplicationRoleValues.Read(
            document, Parse("""{ "ma_tytul": false, "tytul": "Zostało sprzed ukrycia" }"""));

        Assert.Null(values.ProjectTitle);
    }
}
