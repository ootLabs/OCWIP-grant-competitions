using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using Ocwip.Api.Services.Pdf;

namespace Ocwip.Api.Services.Export;

/// <summary>
/// The ranking list written as CSV, XLSX and PDF (T-42a, report: "PDF do
/// publikacji, XLSX i CSV do liczenia").
///
/// XLSX without a library: the file is a ZIP of five small XML parts, and a
/// sheet of text and numbers needs nothing more, so a dependency (and its
/// updates) would buy nothing a table of 120 rows uses. Text goes in as
/// inline strings, which a spreadsheet never evaluates, so a title typed as
/// "=HYPERLINK(...)" stays text here without the apostrophe CSV needs.
/// </summary>
internal static class RankingExportWriters
{
    public static byte[] Csv(RankingExport export)
    {
        var builder = new StringBuilder();
        void Row(IEnumerable<string> cells) =>
            builder.Append(string.Join(';', cells.Select(ApplicationListCsv.Cell))).Append("\r\n");

        Row(export.Columns);
        foreach (var row in export.Rows)
        {
            Row(row.Select(cell => cell.Text));
        }

        builder.Append("\r\n");
        foreach (var (label, amount) in export.Totals)
        {
            Row([label, ApplicationListLabels.Amount(amount)]);
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    public static byte[] Xlsx(RankingExport export)
    {
        var sheet = new StringBuilder();
        sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        var rowNumber = 0;
        void Row(IEnumerable<ExportCell> cells)
        {
            rowNumber++;
            sheet.Append("<row r=\"").Append(rowNumber).Append("\">");
            foreach (var cell in cells)
            {
                if (cell.Number is { } number)
                {
                    sheet.Append("<c><v>").Append(number.ToString(CultureInfo.InvariantCulture)).Append("</v></c>");
                }
                else if (cell.Text.Length > 0)
                {
                    sheet.Append("<c t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                        .Append(Xml(cell.Text))
                        .Append("</t></is></c>");
                }
                else
                {
                    sheet.Append("<c/>");
                }
            }

            sheet.Append("</row>");
        }

        Row(export.Columns.Select(text => new ExportCell(text)));
        foreach (var row in export.Rows)
        {
            Row(row);
        }

        rowNumber++;
        foreach (var (label, amount) in export.Totals)
        {
            Row([new ExportCell(label), ExportCell.Of(amount, string.Empty)]);
        }

        sheet.Append("</sheetData></worksheet>");

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Part(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part(zip, "xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Lista rankingowa" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Part(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Part(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        return stream.ToArray();
    }

    public static byte[] Pdf(RankingExport export)
    {
        // 155 characters of Courier 8 pt across a landscape A4, as in ApplicationListPdfBuilder.
        int[] widths = [4, 9, 28, 34, 8, 12, 12, 12, 28];
        var right = new[] { true, false, false, false, true, true, true, true, false };

        string Line(IReadOnlyList<string> cells) =>
            string.Join(' ', cells.Select((cell, i) =>
            {
                var text = PdfText.Transliterate(cell);
                text = text.Length <= widths[i] ? text : text[..(widths[i] - 3)] + "...";
                return right[i] ? text.PadLeft(widths[i]) : text.PadRight(widths[i]);
            })).TrimEnd();

        string[] headings = ["Lp.", "Numer", "Nazwa podmiotu", "Tytul projektu", "Punkty", "Wnioskowana", "Rekomend.", "Przyznana", "Wynik"];
        var heading = Line(headings);
        var state = export.ApprovedAt is { } approved
            ? $"wyniki zatwierdzone {ApplicationListLabels.Moment(approved)}"
            : "wersja robocza, wyniki niezatwierdzone";
        var header = new[]
        {
            PdfText.Transliterate($"Lista rankingowa, konkurs {export.CompetitionNumber}: {export.CompetitionTitle}"),
            PdfText.Transliterate($"Stan: {state}."),
            heading,
            new string('-', heading.Length),
        };

        var lines = export.Rows.Select(row => Line(row.Select(cell => cell.Text).ToList())).ToList();
        if (lines.Count == 0)
        {
            lines.Add("Brak zlozonych wnioskow.");
        }

        lines.Add(string.Empty);
        foreach (var (label, amount) in export.Totals)
        {
            lines.Add(PdfText.Transliterate(amount is null
                ? $"{label}: nie ustawiono"
                : $"{label}: {ApplicationListLabels.Amount(amount)} zl"));
        }

        return SimplePdfDocument.Create(lines, PdfPageLayout.LandscapeMonospaced, header);
    }

    private static void Part(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>Escaped, and without the control characters XML 1.0 has no way to carry.</summary>
    private static string Xml(string text) =>
        SecurityElement.Escape(new string(text.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray()))!;
}
