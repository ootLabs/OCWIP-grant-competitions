using System.Text;
using System.Text.RegularExpressions;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Pdf;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.FormDefinitionSamples;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// T-44: the printed application carries the printed fields only (D14), only
/// what this applicant was asked, and the checksum on every page (D15).
/// </summary>
public sealed class ApplicationPdfBuilderTests
{
    private static readonly ApplicationPdfFacts Facts = new(
        "7/2026", "Kierunek NOWE FIO", "Stowarzyszenie Łąka", EntityType.Organisation, 3,
        DateTimeOffset.UnixEpoch, "0a55-22c2-b414");

    private static string Pdf(string answers, params string[] fields)
    {
        var form = FormSchemaValidator.Validate(WithFields(fields)).Document!;
        return PdfTextReader.Text(ApplicationPdfBuilder.Build(Facts, form, Parse(answers)));
    }

    [Fact]
    public void Only_printed_fields_the_applicant_was_asked_reach_the_paper()
    {
        var pdf = Pdf(
            """{"tytul":"Lawki w parku","pomocnicze":"ukryte","grupa":"pytanie nie zadane","kwota":6500.5,"zgoda":true,"forma":"stowarzyszenie"}""",
            Field("tytul", "shortText", "\"maxLength\": 200"),
            Field("pomocnicze", "shortText", "\"maxLength\": 200", printed: false),
            Field("forma", "singleChoice", "\"options\": [{ \"value\": \"stowarzyszenie\", \"label\": \"Stowarzyszenie rejestrowe\" }, { \"value\": \"inna\", \"label\": \"Inna\" }]"),
            Field("grupa", "shortText", "\"maxLength\": 200, \"visibleWhen\": { \"field\": \"forma\", \"equalsAnyOf\": [\"inna\"] }"),
            Field("kwota", "amount", "\"minValue\": 0"),
            Field("zgoda", "yesNo"));

        Assert.Contains("Lawki w parku", pdf);
        Assert.Contains("6500,50 zł", pdf);
        Assert.Contains("Tak", pdf);
        Assert.Contains("Stowarzyszenie rejestrowe", pdf);
        Assert.Contains("Wnioskodawca: Stowarzyszenie Łąka", pdf);
        Assert.DoesNotContain("ukryte", pdf);
        Assert.DoesNotContain("pytanie nie zadane", pdf);
        Assert.Contains("Wersja formularza: 3", pdf);
    }

    [Fact]
    public void Every_page_carries_the_checksum()
    {
        var answer = string.Join(' ', Enumerable.Repeat("slowo", 3000));
        var pdf = Pdf($$"""{"opis":"{{answer}}"}""", Field("opis", "longText", "\"maxLength\": 20000"));

        var bytes = ApplicationPdfBuilder.Build(
            Facts,
            FormSchemaValidator.Validate(WithFields(Field("opis", "longText", "\"maxLength\": 20000"))).Document!,
            Parse($$"""{"opis":"{{answer}}"}"""));
        var pages = PdfTextReader.Pages(bytes);
        Assert.True(pages > 1);
        Assert.Equal(pages, Regex.Matches(pdf, @"suma kontrolna 0a55-22c2-b414").Count);
    }

    [Fact]
    public void A_long_line_wraps_under_its_own_indentation()
    {
        var lines = ApplicationPdfBuilder.Wrap("  " + string.Join(' ', Enumerable.Repeat("abcd", 30)), 20).ToList();

        Assert.All(lines, line => Assert.True(line.Length <= 20));
        Assert.All(lines, line => Assert.StartsWith("  abcd", line));
    }

    [Fact]
    public void Pasted_typography_prints_as_pasted_not_as_question_marks()
    {
        var pdf = Pdf(
            """{"opis":"\u201ESpotkania\u201D \u2013 cykl\u2026 dla\u00A0mieszkańców","termin":"2026-05-01T10:00:00Z"}""",
            Field("opis", "longText", "\"maxLength\": 2000"),
            Field("termin", "dateTime"));

        // Printed as pasted since T-45a: the embedded font has these glyphs.
        Assert.Contains("\u201ESpotkania\u201D \u2013 cykl\u2026 dla\u00A0mieszkańców", pdf);
        Assert.DoesNotContain("2026-05-01T10:00:00Z", pdf);
    }
}
