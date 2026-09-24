using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the contract accepts about a percentage ceiling whose percentage is a
/// competition setting, and about a sum of several totals (T-31).
/// </summary>
public sealed class FormLimitContractTests
{
    [Theory]
    [InlineData(
        """{ "kind": "maxPercentOf", "percent": 10, "percentFrom": "competition.maxIndirectCostPercent", "basis": "kwota" }""",
        "procent albo ustawienie konkursu, z którego go wziąć, nie oba naraz")]
    [InlineData(
        """{ "kind": "maxPercentOf", "percentFrom": "competition.maxGrantAmount", "basis": "kwota" }""",
        "nie jest progiem procentowym konkursu")]
    [InlineData(
        """{ "kind": "maxAmount", "percentFrom": "competition.maxIndirectCostPercent", "basis": "kwota" }""",
        "limit kwotowy nie liczy procentu")]
    [InlineData(
        """{ "kind": "maxPercentOf", "basis": "kwota" }""",
        "musi podawać procent albo próg z ustawień konkursu")]
    [InlineData(
        """{ "kind": "maxPercentOf", "percent": 150, "basis": "kwota" }""",
        "procent z przedziału od zera do stu")]
    public void A_percentage_ceiling_is_refused_when_its_percentage_is_ambiguous_or_not_one(
        string limit, string message)
    {
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("kwota", "amount"),
            FormDefinitionSamples.Field("koszty", "amount", $"\"limits\": [{limit}]"));

        var result = FormSchemaValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Message.Contains(message));
    }

    [Fact]
    public void A_column_still_cannot_sum_a_field_outside_its_table()
    {
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("ryczalt", "amount"),
            FormDefinitionSamples.Field(
                "pozycje",
                "repeatableTable",
                $$"""
                "table": {
                  "columns": [
                    {{FormDefinitionSamples.Field("cena", "amount")}},
                    {{FormDefinitionSamples.Field(
                        "razem",
                        "calculated",
                        """
                        "calculation": { "kind": "sum", "operands": ["cena", "ryczalt"] }
                        """)}}
                  ]
                }
                """));

        var result = FormSchemaValidator.Validate(definition);

        Assert.Contains(result.Errors, error => error.Message.Contains("nie jest kolumną tabeli"));
    }
}
