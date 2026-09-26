using System.Text.Json;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Reports;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Services.Reports;

/// <summary>T-50a: the report starts as the application said, and what may not change does not.</summary>
public sealed class ReportPrefillTests
{
    private static readonly FormDocument Report =
        FormSchemaValidator.Validate(ReportFormSamples.Report(), FormPurpose.Report).Document!;

    private static readonly FormDocument Application =
        FormSchemaValidator.Validate(ReportFormSamples.Application()).Document!;

    private static JsonElement Prefill() =>
        JsonSerializer.SerializeToElement(ReportPrefill.Build(
            Report, Application, FormDefinitionSamples.Parse(ReportFormSamples.ApplicationAnswers), EntityType.Organisation));

    [Fact]
    public void The_report_takes_the_title_and_each_budget_row_with_its_computed_value()
    {
        var prefill = Prefill();

        Assert.Equal("Ławki w parku", prefill.GetProperty("tytul").GetString());
        var rows = prefill.GetProperty("budzet").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Deski", rows[0].GetProperty("pozycja").GetString());
        Assert.Equal(1500m, rows[0].GetProperty("planowana").GetDecimal());
        Assert.False(rows[0].TryGetProperty("wykonana", out _));
    }

    [Fact]
    public void A_save_puts_back_what_the_application_said_and_keeps_what_the_applicant_did()
    {
        var tampered = FormDefinitionSamples.Parse("""
            {"tytul":"Inny tytuł","przebieg":"Zrobiliśmy",
             "budzet":[{"pozycja":"Zmienione","planowana":1,"wykonana":1400},
                       {"pozycja":"Dopisane","planowana":999,"wykonana":80}]}
            """);

        var saved = JsonSerializer.SerializeToElement(ReportPrefill.Apply(Report, tampered, Prefill()));

        Assert.Equal("Ławki w parku", saved.GetProperty("tytul").GetString());
        Assert.Equal("Zrobiliśmy", saved.GetProperty("przebieg").GetString());
        var rows = saved.GetProperty("budzet").EnumerateArray().ToList();
        Assert.Equal("Deski", rows[0].GetProperty("pozycja").GetString());
        Assert.Equal(1500m, rows[0].GetProperty("planowana").GetDecimal());
        Assert.Equal(1400m, rows[0].GetProperty("wykonana").GetDecimal());

        // The second row is the application's own: a deleted or renamed row
        // comes back; the applicant's value next to it stays.
        Assert.Equal("Farba", rows[1].GetProperty("pozycja").GetString());
        Assert.Equal(100m, rows[1].GetProperty("planowana").GetDecimal());
        Assert.Equal(80m, rows[1].GetProperty("wykonana").GetDecimal());
    }

    [Fact]
    public void A_row_the_applicant_added_has_no_value_from_the_application()
    {
        var answers = FormDefinitionSamples.Parse("""
            {"budzet":[{"wykonana":1},{"wykonana":2},{"pozycja":"Nowa","planowana":500,"wykonana":300}]}
            """);

        var rows = JsonSerializer.SerializeToElement(ReportPrefill.Apply(Report, answers, Prefill()))
            .GetProperty("budzet").EnumerateArray().ToList();

        Assert.Equal(3, rows.Count);
        Assert.False(rows[2].TryGetProperty("pozycja", out _));
        Assert.False(rows[2].TryGetProperty("planowana", out _));
        Assert.Equal(300m, rows[2].GetProperty("wykonana").GetDecimal());
    }

    [Fact]
    public void A_deleted_application_row_comes_back()
    {
        var answers = FormDefinitionSamples.Parse("""{"budzet":[{"wykonana":1}]}""");

        var rows = JsonSerializer.SerializeToElement(ReportPrefill.Apply(Report, answers, Prefill()))
            .GetProperty("budzet").EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Farba", rows[1].GetProperty("pozycja").GetString());
    }
}
