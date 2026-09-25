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
        new("Rodzaj", 20),
        new("Tytul projektu", 34),
        new("Koszt calkowity", 14, RightAligned: true),
        new("Wnioskowana", 14, RightAligned: true),
        new("Status", 9),
        new("Data zlozenia", 16),
    ];

    private static readonly int LineWidth =
        Columns.Sum(column => column.Width) + Columns.Length - 1;

    public static byte[] Build(ApplicationListResponse list)
    {
        var heading = Line(Columns.Select(column => column.Heading).ToArray());

        var header = new[]
        {
            // A title may run to 200 characters, past the right edge.
            Fit(
                PdfText.Transliterate(
                    $"Lista wniosków, konkurs {list.CompetitionNumber}: {list.CompetitionTitle}"),
                LineWidth),
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
                EntityType(item.EntityType),
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

    /// <summary>
    /// The full "Grupa nieformalna pod patronatem" does not fit the "Rodzaj"
    /// column, and cut to it reads as "Grupa nieformalna...", one ellipsis
    /// away from the group without a patron. The PDF, and only the PDF,
    /// prints a shorter label instead; the screen and the spreadsheet keep
    /// the name from docs/reguly-biznesowe.md.
    ///
    /// The label is exactly as wide as the column (20), so narrowing "Rodzaj"
    /// truncates it silently. Shorten the label in the same change, and let
    /// ApplicationListExportTests say what both kinds print.
    /// </summary>
    private static string EntityType(Models.EntityType type) =>
        type == Models.EntityType.PatronInformalGroup
            ? "Grupa pod patronatem"
            : ApplicationListLabels.EntityType(type);

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
