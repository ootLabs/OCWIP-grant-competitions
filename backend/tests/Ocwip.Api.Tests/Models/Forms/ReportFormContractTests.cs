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

    /// <summary>
    /// O-17: a cell and the contract's date are sources for a plain field.
    /// On a table they would put one value where rows belong, on a column
    /// they would match nothing, and the date fits only a date.
    /// </summary>
    [Fact]
    public void A_cell_or_the_contract_date_feeds_a_plain_field_only()
    {
        var errors = Errors(
            WithFields(
                Field("od", "date", "\"prefillFrom\": \"contract.signedOn\""),
                Field("lider", "shortText", "\"maxLength\": 50, \"prefillFrom\": \"czlonkowie.lider.imie\""),
                Field("od_tekstem", "shortText", "\"maxLength\": 50, \"prefillFrom\": \"contract.signedOn\""),
                Field(
                    "tabela",
                    "repeatableTable",
                    $$"""
                    "prefillFrom": "czlonkowie.lider.imie",
                    "table": { "columns": [ {{Field("pozycja", "shortText", "\"maxLength\": 50, \"prefillFrom\": \"contract.signedOn\"")}} ] }
                    """)),
            FormPurpose.Report);

        Assert.DoesNotContain(errors, error => error.Contains("\"od\"") || error.Contains("\"lider\""));
        Assert.Contains(errors, error => error.Contains("od_tekstem") && error.Contains("tylko do pola daty"));
        Assert.Contains(errors, error => error.Contains("\"tabela\"") && error.Contains("bez komórki"));
        Assert.Contains(errors, error => error.Contains("\"pozycja\"") && error.Contains("bez komórki"));
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

    [Fact]
    public void A_report_marks_its_budget_and_the_grant_column()
    {
        var result = FormSchemaValidator.Validate(ReportFormSamples.SettledReport(), FormPurpose.Report);

        Assert.Empty(result.Errors);
        var table = result.Document!.Sections[0].Fields.Single(field => field.Key == "budzet");
        Assert.Equal(FormFieldRole.ReportBudget, table.Role);
        Assert.Equal(FormFieldRole.GrantSpent, table.Table!.Columns.Single(column => column.Key == "wykonana").Role);
    }

    [Fact]
    public void A_budget_needs_exactly_one_grant_column()
    {
        var none = ReportFormSamples.SettledReport().GetRawText().Replace(", \"role\": \"grantSpent\"", string.Empty);
        var two = ReportFormSamples.SettledReport().GetRawText()
            .Replace("\"key\": \"planowana\",", "\"key\": \"planowana\", \"role\": \"grantSpent\",");

        Assert.Contains(Errors(Parse(none), FormPurpose.Report), error => error.Contains("dokładnie jednej") && error.Contains("ma 0"));
        Assert.Contains(Errors(Parse(two), FormPurpose.Report), error => error.Contains("dokładnie jednej") && error.Contains("ma 2"));
    }

    [Fact]
    public void A_grant_column_belongs_to_the_budget_only()
    {
        var unmarked = ReportFormSamples.SettledReport().GetRawText().Replace(" \"role\": \"reportBudget\",", string.Empty);

        Assert.Contains(Errors(Parse(unmarked), FormPurpose.Report), error => error.Contains("tylko tabela z rolą"));
    }

    [Fact]
    public void Grant_spending_is_a_column_of_amounts_and_the_budget_a_table()
    {
        var errors = Errors(
            WithFields(
                Field("kwota", "amount", "\"minValue\": 0, \"role\": \"grantSpent\""),
                Field("opis", "shortText", "\"maxLength\": 50, \"role\": \"reportBudget\"")),
            FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("kwota") && error.Contains("tylko kolumna"));
        Assert.Contains(errors, error => error.Contains("opis") && error.Contains("tylko tabela"));
    }

    [Fact]
    public void A_report_carries_no_role_of_an_application()
    {
        var errors = Errors(WithFields(Field("koszt", "amount", "\"minValue\": 0, \"role\": \"totalCost\"")), FormPurpose.Report);

        Assert.Contains(errors, error => error.Contains("nosi tylko role"));
    }

    [Theory]
    [InlineData(FormPurpose.Application)]
    [InlineData(FormPurpose.MeritEvaluation)]
    public void Only_a_report_carries_the_budget_roles(FormPurpose purpose)
    {
        var table = ReportFormSamples.SettledReport().GetRawText()
            .Replace("\"prefillFrom\": \"budzet_a\",", string.Empty)
            .Replace("\"readOnly\": true, \"prefillFrom\": \"nazwa\"", "\"readOnly\": false")
            .Replace("\"readOnly\": true, \"prefillFrom\": \"wartosc\"", "\"readOnly\": false")
            .Replace("\"readOnly\": true, \"prefillFrom\": \"opis\"", "\"readOnly\": false");

        var errors = Errors(Parse(table), purpose);

        Assert.Contains(errors, error => error.Contains("\"reportBudget\" należy do wzoru sprawozdania"));
        Assert.Contains(errors, error => error.Contains("\"grantSpent\" należy do wzoru sprawozdania"));
    }

    private static System.Text.Json.JsonElement Parse(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement;
}
