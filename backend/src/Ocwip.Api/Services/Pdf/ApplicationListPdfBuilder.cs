using System.Globalization;
using System.Text;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services.Export;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// The list of applications as a PDF (T-35): a landscape table in a
/// monospaced font, columns padded with spaces, the heading repeated on every
/// page and the totals at the end.
///
/// Transliterated for the reason SimplePdfDocument gives. A text longer than
/// its column is cut with "..." rather than wrapped: the PDF is the list to
/// print and hand over, and a row that breaks into three lines is the one a
/// reader loses; the full text is on the screen and in the spreadsheet.
/// </summary>
internal static class ApplicationListPdfBuilder
{
    private sealed record Column(string Heading, int Width, bool RightAligned = false);

    // 155 characters of Courier 8 pt fit the 770 points of a landscape A4
    // between the margins, separators included.
    private static readonly Column[] Columns =
    [
        new("Lp.", 4, RightAligned: true),
        new("Numer", 6),
        new("Nazwa podmiotu", 30),
        new("Rodzaj", 16),
        new("Tytul projektu", 38),
        new("Koszt calkowity", 14, RightAligned: true),
        new("Wnioskowana", 14, RightAligned: true),
        new("Status", 9),
        new("Data zlozenia", 16),
    ];

    public static byte[] Build(ApplicationListResponse list)
    {
        var heading = Line(Columns.Select(column => column.Heading).ToArray());

        var header = new[]
        {
            PdfText.Transliterate(
                $"Lista wniosków, konkurs {list.CompetitionNumber}: {list.CompetitionTitle}"),
            string.Empty,
            heading,
            new string('-', heading.Length),
        };

        var lines = new List<string>();

        for (var i = 0; i < list.Applications.Count; i++)
        {
            var item = list.Applications[i];
            lines.Add(Line(
                (i + 1).ToString(CultureInfo.InvariantCulture),
                item.Number,
                item.EntityName,
                ApplicationListLabels.EntityType(item.EntityType),
                item.ProjectTitle ?? string.Empty,
                ApplicationListLabels.Amount(item.TotalCost),
                ApplicationListLabels.Amount(item.RequestedGrant),
                ApplicationListLabels.Status(item.Status),
                ApplicationListLabels.Moment(item.SubmittedAt)));
        }

        if (list.Applications.Count == 0)
        {
            lines.Add("Brak zlozonych wnioskow.");
        }

        lines.Add(string.Empty);
        lines.Add(Total("Suma wnioskowanych kwot", list.RequestedTotal));
        lines.Add(Total("Pula konkursu", list.TotalPoolAmount));
        lines.Add(Total("Pozostalo z puli", list.PoolRemaining));
        lines.Add(PdfText.Transliterate($"Daty według {ApplicationListLabels.TimeLabel}."));

        return SimplePdfDocument.Create(lines, PdfPageLayout.LandscapeMonospaced, header);
    }

    private static string Total(string label, decimal? amount) =>
        amount is null
            ? $"{label}: nie ustawiono"
            : $"{label}: {ApplicationListLabels.Amount(amount)} zl";

    private static string Line(params string[] cells)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < Columns.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            var text = Fit(PdfText.Transliterate(cells[i]), Columns[i].Width);
            builder.Append(Columns[i].RightAligned
                ? text.PadLeft(Columns[i].Width)
                : text.PadRight(Columns[i].Width));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text : text[..(width - 3)] + "...";
}
