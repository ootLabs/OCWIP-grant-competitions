using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>Which answers a form makes sensitive (T-47a), before anything is encrypted.</summary>
public sealed class SensitiveAnswersTests
{
    private static FormDocument Parse(System.Text.Json.JsonElement definition, FormPurpose purpose = FormPurpose.Application)
    {
        var result = FormSchemaValidator.Validate(definition, purpose);
        Assert.Empty(result.Errors);
        return result.Document!;
    }

    [Fact]
    public void A_marked_field_and_a_table_with_a_marked_column_are_sensitive()
    {
        var form = Parse(WithFields(
            Field("opis", "shortText", "\"maxLength\": 500"),
            Field("rachunek", "shortText", "\"maxLength\": 40, \"sensitive\": true"),
            Field("czlonkowie", "repeatableTable", $$"""
                "table": { "minRows": 1, "columns": [
                  {{Field("imie", "shortText", "\"maxLength\": 100, \"sensitive\": true")}},
                  {{Field("rola", "shortText", "\"maxLength\": 100")}}
                ] }
                """)));

        Assert.Equal(["czlonkowie", "rachunek"], SensitiveAnswers.Keys(form).Order());
    }

    [Fact]
    public void The_2026_form_protects_the_group_members_and_the_leaders_account()
    {
        Assert.Equal(["czlonkowie_grupy", "rachunek_lidera"], SensitiveAnswers.Keys(Application2026FormTests.Form()).Order());
    }

    [Fact]
    public void A_report_field_filled_from_a_sensitive_answer_is_sensitive_without_its_own_flag()
    {
        var marked = ReportFormSamples.Application().GetRawText()
            .Replace("\"key\": \"opis\",", "\"key\": \"opis\", \"sensitive\": true,");
        var application = Parse(FormDefinitionSamples.Parse(marked));
        var report = Parse(ReportFormSamples.Report(), FormPurpose.Report);

        var keys = SensitiveAnswers.ReportKeys(report, application);

        // "tytul" is prefilled from "opis"; "przebieg" is typed in the report.
        Assert.Contains("tytul", keys);
        Assert.DoesNotContain("przebieg", keys);
        Assert.Empty(SensitiveAnswers.ReportKeys(report, Parse(ReportFormSamples.Application())));
    }
}
