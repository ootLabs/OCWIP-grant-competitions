using System.IO.Compression;
using System.Text;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>The ranking list as CSV, XLSX and PDF (T-42a): one set of rows, three files that agree.</summary>
public sealed class RankingExportWritersTests
{
    private static RankingExport Sample(DateTimeOffset? approvedAt = null, string title = "Kierunek NOWE FIO")
    {
        var row = new RankingRow(
            1, Guid.NewGuid(), "1/2026/1", "Stowarzyszenie \"Łąka\"", EntityType.Organisation,
            "=HYPERLINK(\"http://x\")", 7000m, DateTimeOffset.UnixEpoch, FormalStanding.Passed,
            1, 1, 41m, 2m, 43m, true, false, 6800m, ApplicationStatus.Funded, 6500.5m, null);
        var ranking = new RankingResponse(
            Guid.NewGuid(), null!, [row], TotalPool: 100000m, AwardedTotal: 6500.5m, ResultsApprovedAt: approvedAt);
        return RankingExport.From("1/2026", title, ranking);
    }

    [Fact]
    public void The_csv_keeps_a_formula_typed_by_an_applicant_as_text()
    {
        var text = Encoding.UTF8.GetString(RankingExportWriters.Csv(Sample()));

        Assert.Contains("'=HYPERLINK", text);
        Assert.Contains("6500,50", text);
        Assert.Contains("Pozostało z puli;93499,50", text);
    }

    [Fact]
    public void The_xlsx_is_a_workbook_with_numbers_as_numbers_and_text_as_inline_text()
    {
        using var zip = new ZipArchive(new MemoryStream(RankingExportWriters.Xlsx(Sample(DateTimeOffset.UnixEpoch))));

        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = reader.ReadToEnd();

        // Parses as XML, so a quote or an ampersand in a name cannot break the file.
        System.Xml.Linq.XDocument.Parse(xml);
        Assert.Contains("<v>6500.5</v>", xml);
        Assert.Contains("Stowarzyszenie &quot;Łąka&quot;", xml);
        Assert.Contains("<t xml:space=\"preserve\">=HYPERLINK", xml);
        Assert.Contains("Dofinansowany, umowa niepodpisana", xml);
    }

    [Fact]
    public void The_pdf_says_whether_the_results_are_approved()
    {
        var draft = Encoding.Latin1.GetString(RankingExportWriters.Pdf(Sample()));
        var approved = Encoding.Latin1.GetString(RankingExportWriters.Pdf(Sample(DateTimeOffset.UnixEpoch)));

        Assert.StartsWith("%PDF", draft);
        Assert.Contains("wersja robocza", draft);
        Assert.Contains("wyniki zatwierdzone", approved);
    }

    [Fact]
    public void The_pdf_cuts_a_long_title_instead_of_running_past_the_page()
    {
        var title = new string('x', 190) + "KONIEC";
        var pdf = Encoding.Latin1.GetString(RankingExportWriters.Pdf(Sample(title: title)));

        Assert.DoesNotContain("KONIEC", pdf);
        Assert.Contains("xxx...", pdf);
    }
}
