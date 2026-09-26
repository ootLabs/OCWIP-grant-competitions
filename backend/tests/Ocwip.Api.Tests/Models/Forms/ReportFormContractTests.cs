using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>T-50a: what a report form may carry, and what only a report form may carry.</summary>
public sealed class ReportFormContractTests
{
    private static IReadOnlyList<string> Errors(System.Text.Json.JsonElement document, FormPurpose purpose) =>
        FormSchemaValidator.Validate(document, purpose).Errors.Select(error => error.Path + " " + error.Message).ToList();

    [Fact]
    public void The_sample_report_passes_and_keeps_its_sources()
    {
        var result = FormSchemaValidator.Validate(ReportFormSamples.Report(), FormPurpose.Report);

        Assert.Empty(result.Errors);
        var table = result.Document!.Sections[0].Fields.Single(field => field.Key == "budzet");
        Assert.Equal("budzet_a", table.PrefillFrom);
        Assert.True(table.Table!.Columns.Single(column => column.Key == "planowana").ReadOnly);
    }

    [Fact]
    public void Only_a_report_takes_values_from_the_application()
    {
        var errors = Errors(WithFields(Field("tytul", "shortText", "\"maxLength\": 50, \"readOnly\": true, \"prefillFrom\": \"opis\"")), FormPurpose.Application);

        Assert.Contains(errors, error => error.Contains("readOnly") && error.Contains("sprawozdania"));
        Assert.Contains(errors, error => error.Contains("prefillFrom") && error.Contains("sprawozdania"));
    }

    [Fact]
    public void A_read_only_value_needs_a_source_and_a_computed_one_needs_none()
    {
        var errors = Errors(
            WithFields(
                Field("bez_zrodla", "shortText", "\"maxLength\": 50, \"readOnly\": true"),
                Field("liczba", "number", "\"minValue\": 0"),
                Field("wyliczone", "calculated", "\"calculation\": { \"kind\": \"sum\", \"operands\": [\"liczba\"] }, \"readOnly\": true, \"prefillFrom\": \"x\"")),
            FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("bez_zrodla") && error.Contains("nie miałoby skąd"));
        Assert.Contains(errors, error => error.Contains("wyliczone") && error.Contains("wyliczane"));
    }

    [Fact]
    public void A_column_is_taken_from_the_application_only_inside_a_table_that_is()
    {
        var errors = Errors(
            WithFields(Field(
                "tabela",
                "repeatableTable",
                $$"""
                "table": { "columns": [ {{Field("pozycja", "shortText", "\"maxLength\": 50, \"readOnly\": true, \"prefillFrom\": \"nazwa\"")}} ] }
                """)),
            FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("pozycja") && error.Contains("tabeli, która sama ma"));
    }

    [Fact]
    public void A_report_scores_nothing()
    {
        var errors = Errors(WithFields(Field("tak", "yesNo", "\"points\": 1")), FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("nie przyznaje punktów"));
    }

    [Fact]
    public void A_read_only_field_may_not_be_required()
    {
        var errors = Errors(
            WithFields(Field("tytul", "shortText", "\"maxLength\": 50, \"readOnly\": true, \"prefillFrom\": \"opis\"")),
            FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("tytul") && error.Contains("nie może być wymagane"));
    }
}
