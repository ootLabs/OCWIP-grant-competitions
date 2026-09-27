using System.Text;
using System.Text.RegularExpressions;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;
using Ocwip.Api.Services.Pdf;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The list of applications as a spreadsheet and as a PDF (T-35).
/// </summary>
public sealed class ApplicationListExportTests
{
    private static ApplicationListItem Item(int index, string? title = "Warsztaty", decimal? grant = 1000m) =>
        new(
            Guid.NewGuid(),
            index.ToString("D3"),
            $"Stowarzyszenie {index}",
            EntityType.Organisation,
            title,
            1500.125m,
            grant,
            ApplicationStatus.Submitted,
            new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.Zero));

    private static ApplicationListResponse List(decimal? pool, params ApplicationListItem[] items)
    {
        var requested = items.Sum(item => item.RequestedGrant ?? 0m);
        return new ApplicationListResponse(
            Guid.NewGuid(), "1/2026", "Konkurs żółty", pool, requested, pool - requested, items);
    }

    [Fact]
    public void The_spreadsheet_opens_as_polish_text_with_amounts_as_numbers()
    {
        var bytes = ApplicationListCsv.Build(List(1500m, Item(1), Item(2, title: null, grant: null)));

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n");

        Assert.Equal(
            "Lp.;Numer wniosku;Nazwa podmiotu;Rodzaj wnioskodawcy;Tytuł projektu;"
            + "Całkowity koszt zadania;Wnioskowana kwota;Status;Data złożenia",
            lines[0]);
        Assert.StartsWith("1;001;Stowarzyszenie 1;Organizacja;Warsztaty;1500,13;1000,00;Złożony;2026-09-15 ", lines[1]);
        Assert.StartsWith("2;002;Stowarzyszenie 2;Organizacja;;1500,13;;Złożony;", lines[2]);
        Assert.Contains("Suma wnioskowanych kwot;1000,00", lines);
        Assert.Contains("Pula konkursu;1500,00", lines);
        Assert.Contains("Pozostało z puli;500,00", lines);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+48 123", "'+48 123")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("Dom; kultura", "\"Dom; kultura\"")]
    [InlineData("-500,00", "-500,00")]
    [InlineData("Zwykły tytuł", "Zwykły tytuł")]
    public void A_cell_can_neither_split_the_row_nor_run_as_a_formula(string value, string expected)
    {
        Assert.Equal(expected, ApplicationListCsv.Cell(value));
    }

    [Fact]
    public void An_unset_pool_is_left_empty_rather_than_read_as_zero()
    {
        var text = Encoding.UTF8.GetString(ApplicationListCsv.Build(List(null, Item(1))));

        Assert.Contains("Pula konkursu;\r\n", text);
        Assert.Contains("Pozostało z puli;\r\n", text);
    }

    [Fact]
    public void A_hundred_and_twenty_rows_go_onto_several_pages_each_with_its_heading()
    {
        var items = Enumerable.Range(1, 120).Select(i => Item(i)).ToArray();

        var bytes = ApplicationListPdfBuilder.Build(List(200000m, items));
        var text = PdfTextReader.Text(bytes);

        Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(bytes, 0, 8));

        // 47 lines a page, 4 of them the repeated heading: 120 rows and the
        // five lines of totals need three pages.
        Assert.Equal(3, PdfTextReader.Pages(bytes));
        Assert.Equal(3, Regex.Matches(text, @"^ *Lp\.", RegexOptions.Multiline).Count);
        Assert.Matches(new Regex(@"^ 120 120", RegexOptions.Multiline), text);

        // Polish as typed, since T-45a: no transliteration left.
        Assert.Contains("Konkurs żółty", text);
        Assert.Contains("Suma wnioskowanych kwot: 120000,00 zł", text);
    }

    [Fact]
    public void The_two_kinds_of_informal_group_stay_apart_in_the_pdf()
    {
        var items = new[]
        {
            Item(1) with { EntityType = EntityType.InformalGroup },
            Item(2) with { EntityType = EntityType.PatronInformalGroup },
        };

        var pdf = PdfTextReader.Text(ApplicationListPdfBuilder.Build(List(null, items)));

        Assert.Matches(@"Stowarzyszenie 1 +Grupa nieformalna ", pdf);
        Assert.Matches(@"Stowarzyszenie 2 +Grupa pod patronatem ", pdf);
    }

    [Fact]
    public void A_long_competition_title_is_cut_to_the_width_of_the_table()
    {
        var list = List(null, Item(1)) with { CompetitionTitle = new string('x', 200) };

        var pdf = PdfTextReader.Text(ApplicationListPdfBuilder.Build(list));

        var title = pdf.Split('\n').First(line => line.StartsWith("Lista wniosków", StringComparison.Ordinal));
        Assert.Equal(155, title.Length);
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void The_confirmation_slip_is_still_one_portrait_page()
    {
        var pdf = Encoding.ASCII.GetString(SimplePdfDocument.Create(["Potwierdzenie"]));

        Assert.Contains("/Count 1", pdf);
        Assert.Contains("/MediaBox [0 0 595 842]", pdf);
        Assert.Contains("/BaseFont /NotoSans-Regular", pdf);
        Assert.Contains("/FontFile2", pdf);
    }

    [Fact]
    public void Polish_letters_survive_the_round_trip_and_an_unknown_character_shows_as_a_question_mark()
    {
        var bytes = SimplePdfDocument.Create(["Zażółć gęślą jaźń", "ZAŻÓŁĆ GĘŚLĄ JAŹŃ", "Znak \U0001F600 spoza czcionki"]);

        Assert.Equal(
            "Zażółć gęślą jaźń\nZAŻÓŁĆ GĘŚLĄ JAŹŃ\nZnak ? spoza czcionki",
            PdfTextReader.Text(bytes));
    }
}
