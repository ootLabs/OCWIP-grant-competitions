using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the contract accepts about the role marker the operator's list of
/// applications reads its title, cost and grant columns from (T-35).
/// </summary>
public sealed class FormFieldRoleContractTests
{
    [Fact]
    public void The_three_roles_are_read_from_the_fields_that_carry_them()
    {
        var definition = WithFields(
            Field("tytul", "shortText", "\"maxLength\": 200, \"role\": \"projectTitle\""),
            Field("koszt", "amount", "\"role\": \"totalCost\""),
            Field(
                "dotacja",
                "calculated",
                """
                "calculation": { "kind": "difference", "operands": ["koszt", "wklad"] },
                "role": "requestedGrant"
                """),
            Field("wklad", "amount"));

        var result = FormSchemaValidator.Validate(definition);

        Assert.Empty(result.Errors);
        var fields = result.Document!.Sections[0].Fields;
        Assert.Equal(FormFieldRole.ProjectTitle, fields[0].Role);
        Assert.Equal(FormFieldRole.TotalCost, fields[1].Role);
        Assert.Equal(FormFieldRole.RequestedGrant, fields[2].Role);
        Assert.Equal(FormFieldRole.None, fields[3].Role);
    }

    [Theory]
    [InlineData("shortText", "\"maxLength\": 20, \"role\": \"kwota\"", "nie istnieje w kontrakcie")]
    [InlineData("longText", "\"maxLength\": 20, \"role\": \"projectTitle\"", "tylko pole tekstu krótkiego")]
    [InlineData("shortText", "\"maxLength\": 20, \"role\": \"totalCost\"", "tylko pole kwoty albo pole wyliczane")]
    [InlineData("number", "\"role\": \"requestedGrant\"", "tylko pole kwoty albo pole wyliczane")]
    public void A_role_that_does_not_exist_or_does_not_fit_the_field_is_refused(
        string type, string extra, string message)
    {
        var definition = WithFields(Field("pole", type, extra));

        var result = FormSchemaValidator.Validate(definition);

        var error = Assert.Single(result.Errors);
        Assert.Equal("$.sections[0].fields[0].role", error.Path);
        Assert.Contains(message, error.Message);
    }

    // The parser reads the kind without regard to case, so "Ratio" is a
    // percentage just as much as "ratio" is.
    [Theory]
    [InlineData("ratio")]
    [InlineData("Ratio")]
    public void A_percentage_cannot_stand_for_an_amount(string kind)
    {
        var definition = WithFields(
            Field("a", "amount"),
            Field("b", "amount"),
            Field(
                "udzial",
                "calculated",
                $$"""
                "calculation": { "kind": "{{kind}}", "operands": ["a", "b"] },
                "role": "requestedGrant"
                """));

        var result = FormSchemaValidator.Validate(definition);

        Assert.Contains(result.Errors, error =>
            error.Path == "$.sections[0].fields[2].role"
            && error.Message.Contains("nie jest procentem"));
    }

    [Fact]
    public void A_table_column_cannot_carry_a_role()
    {
        var definition = WithFields(
            Field(
                "budzet",
                "repeatableTable",
                $$"""
                "table": {
                  "columns": [{{Field("cena", "amount", "\"role\": \"totalCost\"")}}]
                }
                """));

        var result = FormSchemaValidator.Validate(definition);

        var error = Assert.Single(result.Errors);
        Assert.Equal("$.sections[0].fields[0].table.columns[0].role", error.Path);
        Assert.Contains("Kolumna \"cena\" nie może mieć roli", error.Message);
    }

    [Fact]
    public void A_role_given_to_a_second_field_is_refused_on_the_second_one()
    {
        var definition = WithFields(
            Field("tytul", "shortText", "\"maxLength\": 200, \"role\": \"projectTitle\""),
            Field("podtytul", "shortText", "\"maxLength\": 200, \"role\": \"projectTitle\""));

        var result = FormSchemaValidator.Validate(definition);

        var error = Assert.Single(result.Errors);
        Assert.Equal("$.sections[0].fields[1].role", error.Path);
        Assert.Contains("może wystąpić najwyżej raz", error.Message);
    }
}
