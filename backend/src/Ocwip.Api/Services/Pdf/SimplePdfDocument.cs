using System.Globalization;
using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// How the text sits on the page. The confirmation slip is a few lines of
/// proportional text (Noto Sans; "Helvetica" names it) on a portrait page; the list of applications (T-35) is a table
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
/// The project references no PDF package (Ocwip.Api.csproj), and every
/// document it prints is lines of text: exactly the shape that does not need
/// a layout engine or a NuGet dependency that drags in native binaries.
///
/// Polish text is written as it is (T-45a): the font is embedded, Noto Sans
/// or Noto Sans Mono from Assets/Fonts (SIL OFL 1.1), as a CID font with the
/// Identity-H encoding, so a line is a list of glyph numbers, with a ToUnicode
/// map that keeps the text copyable and searchable. The whole font file goes
/// in, compressed: subsetting would save a few hundred kilobytes per file at
/// the cost of rewriting glyph tables, and nothing here is sent in bulk yet.
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

        var font = layout.Font == "Courier"
            ? TrueTypeFont.Embedded("NotoSansMono-Regular.ttf", "NotoSansMono-Regular")
            : TrueTypeFont.Embedded("NotoSans-Regular.ttf", "NotoSans-Regular");

        // Every glyph the pages use, with the text it draws, for the widths
        // and for the ToUnicode map that makes the text copyable.
        var used = new SortedDictionary<ushort, string>();
        var contents = pages.Select(page => BuildContentStream(page, layout, font, used)).ToList();

        // 1 catalog, 2 page tree, 3 font (Type0), 4 its CID font, 5 the font
        // descriptor, 6 the font file, 7 the ToUnicode map; then a page object
        // and its content stream for every page.
        const int firstPage = 8;
        var kids = string.Join(' ', pages.Select((_, i) => $"{firstPage + 2 * i} 0 R"));

        using var buffer = new MemoryStream();
        var offsets = new List<int>();

        void WriteAscii(string text) =>
            buffer.Write(Encoding.ASCII.GetBytes(text));

        void WriteObject(int number, string body)
        {
            offsets.Add((int)buffer.Length);
            WriteAscii($"{number} 0 obj\n{body}\nendobj\n");
        }

        void WriteStream(int number, string dictionary, byte[] content)
        {
            offsets.Add((int)buffer.Length);
            WriteAscii($"{number} 0 obj\n<< {dictionary} /Length {content.Length} >>\nstream\n");
            buffer.Write(content);
            WriteAscii("\nendstream\nendobj\n");
        }

        var bbox = string.Join(' ', font.BoundingBox.Select(value => font.Scale(value)));
        var widths = string.Join(' ', used.Keys.Select(glyph => $"{glyph} [{font.Width(glyph)}]"));

        WriteAscii("%PDF-1.4\n");

        // The customary second line of bytes above 127: it tells a transfer
        // tool the file is binary (the font inside it is).
        buffer.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);
        WriteObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(2, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        WriteObject(
            3,
            $"<< /Type /Font /Subtype /Type0 /BaseFont /{font.PostScriptName} /Encoding /Identity-H "
            + "/DescendantFonts [4 0 R] /ToUnicode 7 0 R >>");
        WriteObject(
            4,
            $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{font.PostScriptName} "
            + "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> "
            + $"/FontDescriptor 5 0 R /CIDToGIDMap /Identity /DW 1000 /W [{widths}] >>");
        WriteObject(
            5,
            $"<< /Type /FontDescriptor /FontName /{font.PostScriptName} /Flags 32 /FontBBox [{bbox}] "
            + $"/ItalicAngle 0 /Ascent {font.Scale(font.Ascent)} /Descent {font.Scale(font.Descent)} "
            + $"/CapHeight {font.Scale(font.CapHeight)} /StemV 80 /FontFile2 6 0 R >>");
        WriteStream(6, $"/Filter /FlateDecode /Length1 {font.Data.Length}", Compress(font.Data));
        WriteStream(7, string.Empty, Encoding.ASCII.GetBytes(ToUnicode(used)));

        for (var i = 0; i < pages.Count; i++)
        {
            var page = firstPage + 2 * i;
            WriteObject(
                page,
                "<< /Type /Page /Parent 2 0 R "
                + "/Resources << /Font << /F1 3 0 R >> >> "
                + $"/MediaBox [0 0 {layout.Width} {layout.Height}] /Contents {page + 1} 0 R >>");

            // Left uncompressed on purpose: the text is glyph numbers in hex,
            // readable next to the ToUnicode map, which the tests rely on.
            WriteStream(page + 1, string.Empty, Encoding.ASCII.GetBytes(contents[i]));
        }

        WriteXref(buffer, offsets, WriteAscii);

        return buffer.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>
    /// The map from glyph back to text: without it a reader shows the right
    /// shapes, but copying or searching the document gives nonsense.
    /// </summary>
    private static string ToUnicode(SortedDictionary<ushort, string> used)
    {
        var builder = new StringBuilder();
        builder.Append("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n")
            .Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n")
            .Append("/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n")
            .Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");

        foreach (var chunk in used.Chunk(100))
        {
            builder.Append(chunk.Length).Append(" beginbfchar\n");
            foreach (var (glyph, text) in chunk)
            {
                builder.Append('<').Append(glyph.ToString("X4", CultureInfo.InvariantCulture)).Append("> <")
                    .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))).Append(">\n");
            }

            builder.Append("endbfchar\n");
        }

        builder.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend");
        return builder.ToString();
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

    private static string BuildContentStream(
        IReadOnlyList<string> lines, PdfPageLayout layout, TrueTypeFont font, SortedDictionary<ushort, string> used)
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
            builder.Append('<').Append(Encode(line, font, used)).Append("> Tj\n");
        }

        builder.Append("ET");

        return builder.ToString();
    }

    /// <summary>
    /// A line as glyph numbers, two bytes each (Identity-H). A character the
    /// font cannot draw becomes "?" rather than vanishing, so the reader sees
    /// that something was there.
    /// </summary>
    private static string Encode(string text, TrueTypeFont font, SortedDictionary<ushort, string> used)
    {
        var builder = new StringBuilder(text.Length * 4);
        var fallback = font.Glyph('?') ?? 0;

        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = font.Glyph(rune.Value);
            var drawn = glyph is null ? "?" : rune.ToString();
            var id = glyph ?? fallback;
            used.TryAdd(id, drawn);
            builder.Append(id.ToString("X4", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
