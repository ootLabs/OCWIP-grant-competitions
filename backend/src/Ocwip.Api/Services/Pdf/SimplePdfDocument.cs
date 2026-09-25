using System.Globalization;
using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// How the text sits on the page. The confirmation slip is a few lines of
/// Helvetica on a portrait page; the list of applications (T-35) is a table
/// of 120 rows, which needs a landscape page and a monospaced font so that
/// columns padded with spaces line up.
/// </summary>
internal sealed record PdfPageLayout(
    int Width,
    int Height,
    string Font,
    int FontSize,
    int LineHeight,
    int Margin)
{
    // A4 at 72 points per inch.
    public static readonly PdfPageLayout Portrait =
        new(595, 842, "Helvetica", 11, 16, 56);

    public static readonly PdfPageLayout LandscapeMonospaced =
        new(842, 595, "Courier", 8, 11, 36);

    public int LinesPerPage => (Height - 2 * Margin) / LineHeight;
}

/// <summary>
/// Writes a minimal PDF of left aligned text lines, byte for byte, with no
/// external library.
///
/// The project references no PDF package today (Ocwip.Api.csproj), and both
/// documents it prints are lines of text: exactly the shape that does not
/// need a layout engine, a font embedder or a NuGet dependency that drags in
/// native binaries.
///
/// Base14 fonts only understand WinAnsiEncoding, which has no Polish
/// diacritics, and embedding a real font to fix that is a lot of machinery
/// for lines of text. The caller is expected to hand this class ASCII text;
/// see PdfText.Transliterate. That is a documented narrowing of these PDFs,
/// not a rule for the rest of the product: every other document in this
/// codebase stays Polish, per AGENTS.md.
/// </summary>
internal static class SimplePdfDocument
{
    /// <summary>One portrait page, the confirmation slip (T-33).</summary>
    public static byte[] Create(IReadOnlyList<string> lines) =>
        Create(lines, PdfPageLayout.Portrait);

    /// <summary>
    /// As many pages as the lines need. <paramref name="header"/> is repeated
    /// at the top of every page, so a column heading never ends up on the
    /// first page only.
    /// </summary>
    public static byte[] Create(
        IReadOnlyList<string> lines,
        PdfPageLayout layout,
        IReadOnlyList<string>? header = null)
    {
        header ??= [];
        var perPage = Math.Max(1, layout.LinesPerPage - header.Count);
        var pages = new List<IReadOnlyList<string>>();

        for (var start = 0; start < lines.Count || pages.Count == 0; start += perPage)
        {
            pages.Add([.. header, .. lines.Skip(start).Take(perPage)]);
        }

        // Object 1 is the catalog, 2 the page tree, 3 the font; then a page
        // object and its content stream for every page.
        var kids = string.Join(' ', pages.Select((_, i) => $"{4 + 2 * i} 0 R"));

        using var buffer = new MemoryStream();
        var offsets = new List<int>();

        void WriteAscii(string text) =>
            buffer.Write(Encoding.ASCII.GetBytes(text));

        void WriteObject(int number, string body)
        {
            offsets.Add((int)buffer.Length);
            WriteAscii($"{number} 0 obj\n{body}\nendobj\n");
        }

        WriteAscii("%PDF-1.4\n");
        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(2, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        WriteObject(
            3,
            $"<< /Type /Font /Subtype /Type1 /BaseFont /{layout.Font} "
            + "/Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < pages.Count; i++)
        {
            var page = 4 + 2 * i;
            WriteObject(
                page,
                "<< /Type /Page /Parent 2 0 R "
                + "/Resources << /Font << /F1 3 0 R >> >> "
                + $"/MediaBox [0 0 {layout.Width} {layout.Height}] /Contents {page + 1} 0 R >>");

            // The content stream's body is bytes, not a dictionary string, so
            // it is written by hand rather than through WriteObject.
            var content = Encoding.ASCII.GetBytes(BuildContentStream(pages[i], layout));
            offsets.Add((int)buffer.Length);
            WriteAscii($"{page + 1} 0 obj\n<< /Length {content.Length} >>\nstream\n");
            buffer.Write(content);
            WriteAscii("\nendstream\nendobj\n");
        }

        WriteXref(buffer, offsets, WriteAscii);

        return buffer.ToArray();
    }

    /// <summary>
    /// The cross reference table and trailer, both mandatory: a PDF without a
    /// correct byte offset for every object is not one a reader has to accept,
    /// only one some readers tolerate by falling back to reconstruction.
    /// </summary>
    private static void WriteXref(
        MemoryStream buffer, IReadOnlyList<int> offsets, Action<string> writeAscii)
    {
        var xrefOffset = (int)buffer.Length;

        // Plus one for object 0, the head of the free list every PDF xref
        // table starts with.
        var objectCount = offsets.Count + 1;

        writeAscii($"xref\n0 {objectCount}\n0000000000 65535 f \n");

        foreach (var offset in offsets)
        {
            writeAscii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        writeAscii(
            $"trailer\n<< /Size {objectCount} /Root 1 0 R >>\n"
            + $"startxref\n{xrefOffset}\n%%EOF");
    }

    private static string BuildContentStream(IReadOnlyList<string> lines, PdfPageLayout layout)
    {
        var builder = new StringBuilder();

        builder.Append("BT\n");
        builder.Append("/F1 ").Append(layout.FontSize).Append(" Tf\n");
        builder.Append(layout.LineHeight).Append(" TL\n");
        builder.Append(layout.Margin).Append(' ')
            .Append(layout.Height - layout.Margin).Append(" Td\n");

        var first = true;

        foreach (var line in lines)
        {
            if (!first)
            {
                builder.Append("T*\n");
            }

            first = false;
            builder.Append('(').Append(Escape(line)).Append(") Tj\n");
        }

        builder.Append("ET");

        return builder.ToString();
    }

    /// <summary>
    /// PDF's literal string syntax reserves three ASCII characters. Anything
    /// outside ASCII is the caller's problem to have removed already: this
    /// class writes bytes as given, it does not transliterate.
    /// </summary>
    private static string Escape(string text) =>
        text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
}
