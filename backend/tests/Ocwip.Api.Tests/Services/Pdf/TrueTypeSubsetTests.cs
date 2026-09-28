using System.Buffers.Binary;
using Ocwip.Api.Services.Documents;
using Ocwip.Api.Services.Pdf;
using Ocwip.Api.Tests.Services.Documents;
using Xunit;

namespace Ocwip.Api.Tests.Services.Pdf;

/// <summary>
/// T-45c: a PDF embeds only the glyphs it draws. The numbers stay, the
/// outlines of the rest are emptied, a compound letter keeps its parts, and
/// the file is still a valid TrueType font.
/// </summary>
public sealed class TrueTypeSubsetTests
{
    private static readonly TrueTypeFont Full = TrueTypeFont.Embedded("NotoSans-Regular.ttf", "NotoSans-Regular");

    private static ushort[] GlyphsOf(string text) =>
        [.. text.EnumerateRunes().Select(rune => Full.Glyph(rune.Value)!.Value).Distinct()];

    private static Dictionary<string, (int Offset, int Length)> Tables(byte[] font)
    {
        var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        for (var i = 0; i < BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4)); i++)
        {
            var record = 12 + 16 * i;
            tables[System.Text.Encoding.ASCII.GetString(font, record, 4)] = (
                (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8)),
                (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12)));
        }

        return tables;
    }

    /// <summary>Where the outline of a glyph sits in glyf; the subset always writes a long loca.</summary>
    private static (int Start, int Length) Outline(byte[] font, int glyph)
    {
        var tables = Tables(font);
        var loca = tables["loca"].Offset;
        var start = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(loca + 4 * glyph));
        var end = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(loca + 4 * (glyph + 1)));
        return (tables["glyf"].Offset + start, end - start);
    }

    [Fact]
    public void The_used_glyphs_keep_their_numbers_widths_and_outlines_and_the_rest_are_empty()
    {
        var text = "Zażółć gęślą jaźń ĄĘŚĆŹŻŁÓŃ § 1.";
        var subset = TrueTypeSubset.Create(Full.Data, GlyphsOf(text));
        var read = TrueTypeFont.FromData("NotoSans-Regular", subset);

        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = Full.Glyph(rune.Value)!.Value;
            Assert.Equal(glyph, read.Glyph(rune.Value));
            Assert.Equal(Full.Width(glyph), read.Width(glyph));
            if (rune.Value != ' ')
            {
                Assert.True(Outline(subset, glyph).Length > 0, $"{rune} lost its outline");
            }
        }

        Assert.Equal(0, Outline(subset, Full.Glyph('Q')!.Value).Length);
        Assert.True(Outline(subset, 0).Length > 0, ".notdef must stay");
        Assert.True(subset.Length < Full.Data.Length / 4);
    }

    [Fact]
    public void A_compound_letter_keeps_the_glyphs_it_is_built_from()
    {
        var subset = TrueTypeSubset.Create(Full.Data, GlyphsOf("ąęńśźżćół"));
        var compounds = 0;

        foreach (var glyph in GlyphsOf("ąęńśźżćół"))
        {
            var (start, length) = Outline(subset, glyph);
            if (length < 10 || BinaryPrimitives.ReadInt16BigEndian(subset.AsSpan(start)) >= 0)
            {
                continue;
            }

            compounds++;
            var component = BinaryPrimitives.ReadUInt16BigEndian(subset.AsSpan(start + 12));
            Assert.True(Outline(subset, component).Length > 0, $"component {component} of glyph {glyph} was emptied");
        }

        Assert.True(compounds > 0, "the sample has no compound glyph, so it tests nothing");
    }

    [Fact]
    public void The_file_checksum_adds_up()
    {
        var subset = TrueTypeSubset.Create(Full.Data, GlyphsOf("Umowa"));
        uint sum = 0;
        for (var i = 0; i < subset.Length; i += 4)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(subset.AsSpan(i)));
        }

        Assert.Equal(0xB1B0AFBAu, sum);
    }

    [Fact]
    public void The_2026_contract_is_at_most_half_the_size_it_was_and_still_reads()
    {
        var text = TemplatePlaceholders.Fill(Contract2026TemplateTests.Template(), new Dictionary<string, string?>());
        var lines = text.Split('\n').Select(PdfText.Printable).ToList();
        var pdf = SimplePdfDocument.Create(lines, PdfPageLayout.Portrait, ["Umowa nr 1/2026/001", string.Empty]);

        // The whole font compressed was about 300 KB before T-45c.
        Assert.True(pdf.Length < 150_000, $"the contract PDF has {pdf.Length} bytes");
        var read = PdfTextReader.Text(pdf);
        Assert.Contains("Opolskim Centrum Wspierania Inicjatyw Pozarządowych", read.Replace("\n", " "));
        Assert.Matches(@"/BaseFont /[A-Z]{6}\+NotoSans-Regular", System.Text.Encoding.ASCII.GetString(pdf));
    }
}
