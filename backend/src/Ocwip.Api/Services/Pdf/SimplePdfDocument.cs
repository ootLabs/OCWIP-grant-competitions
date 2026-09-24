using System.Globalization;
using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// Writes a minimal single page PDF of left aligned text lines, byte for
/// byte, with no external library.
///
/// The project references no PDF package today (Ocwip.Api.csproj), and a
/// confirmation slip is a handful of lines on one page: exactly the shape
/// that does not need a layout engine, a font embedder or a NuGet dependency
/// that drags in native binaries for the sake of one document type.
///
/// Base14 fonts only understand WinAnsiEncoding, which has no Polish
/// diacritics, and embedding a real font to fix that is a lot of machinery
/// for one page of text. The caller is expected to hand this class ASCII
/// text; see ApplicationConfirmationPdfBuilder, which transliterates before
/// the text reaches here. That is a documented narrowing of this one PDF, not
/// a rule for the rest of the product: every other document in this codebase
/// stays Polish, per AGENTS.md.
/// </summary>
internal static class SimplePdfDocument
{
    // A4 at 72 points per inch.
    private const int PageWidth = 595;
    private const int PageHeight = 842;

    private const int LeftMargin = 56;
    private const int TopMargin = 56;
    private const int FontSize = 11;
    private const int LineHeight = 16;

    public static byte[] Create(IReadOnlyList<string> lines)
    {
        var contentBytes = Encoding.ASCII.GetBytes(BuildContentStream(lines));

        // Objects 1 to 3: the fixed scaffolding every single page PDF needs.
        // Object 4 (the content stream) and object 5 (the font) are written
        // separately below because the content stream's body is bytes, not a
        // dictionary string.
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R "
                + "/Resources << /Font << /F1 5 0 R >> >> "
                + $"/MediaBox [0 0 {PageWidth} {PageHeight}] /Contents 4 0 R >>",
        };

        using var buffer = new MemoryStream();
        var offsets = new List<int>();

        void WriteAscii(string text) =>
            buffer.Write(Encoding.ASCII.GetBytes(text));

        WriteAscii("%PDF-1.4\n");

        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add((int)buffer.Length);
            WriteAscii($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        offsets.Add((int)buffer.Length);
        WriteAscii($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        buffer.Write(contentBytes);
        WriteAscii("\nendstream\nendobj\n");

        offsets.Add((int)buffer.Length);
        WriteAscii(
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica "
            + "/Encoding /WinAnsiEncoding >>\nendobj\n");

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

    private static string BuildContentStream(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();

        builder.Append("BT\n");
        builder.Append("/F1 ").Append(FontSize).Append(" Tf\n");
        builder.Append(LineHeight).Append(" TL\n");
        builder.Append(LeftMargin).Append(' ')
            .Append(PageHeight - TopMargin).Append(" Td\n");

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
