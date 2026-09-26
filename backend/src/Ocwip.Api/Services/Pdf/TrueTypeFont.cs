using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// The little of a TrueType file a PDF needs to embed it (T-45a): which glyph
/// draws a character (cmap), how wide it is (hmtx) and the metrics of the
/// font descriptor (head, hhea, OS/2). Read once per font and kept, the file
/// itself goes into the PDF whole.
/// </summary>
internal sealed class TrueTypeFont
{
    private static readonly ConcurrentDictionary<string, TrueTypeFont> Loaded = new(StringComparer.Ordinal);

    private readonly Dictionary<int, ushort> _glyphs;
    private readonly ushort[] _advances;

    private TrueTypeFont(string postScriptName, byte[] data)
    {
        PostScriptName = postScriptName;
        Data = data;

        var tables = Tables(data);
        var head = tables["head"];
        UnitsPerEm = U16(data, head + 18);
        BoundingBox = [S16(data, head + 36), S16(data, head + 38), S16(data, head + 40), S16(data, head + 42)];

        var hhea = tables["hhea"];
        Ascent = S16(data, hhea + 4);
        Descent = S16(data, hhea + 6);
        var metrics = U16(data, hhea + 34);

        var glyphCount = U16(data, tables["maxp"] + 4);
        _advances = new ushort[glyphCount];
        var hmtx = tables["hmtx"];
        for (var i = 0; i < glyphCount; i++)
        {
            // Past numberOfHMetrics every glyph repeats the last advance.
            _advances[i] = U16(data, hmtx + 4 * Math.Min(i, metrics - 1));
        }

        CapHeight = tables.TryGetValue("OS/2", out var os2) && U16(data, os2) >= 2 ? S16(data, os2 + 88) : Ascent;
        _glyphs = Cmap(data, tables["cmap"]);
    }

    public string PostScriptName { get; }

    public byte[] Data { get; }

    public int UnitsPerEm { get; }

    public short[] BoundingBox { get; }

    public short Ascent { get; }

    public short Descent { get; }

    public short CapHeight { get; }

    /// <summary>A font from Assets/Fonts, embedded in the assembly.</summary>
    public static TrueTypeFont Embedded(string fileName, string postScriptName) =>
        Loaded.GetOrAdd(fileName, name =>
        {
            using var stream = typeof(TrueTypeFont).Assembly.GetManifestResourceStream($"Fonts.{name}")
                ?? throw new InvalidOperationException($"The font {name} is not embedded in the assembly.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return new TrueTypeFont(postScriptName, copy.ToArray());
        });

    /// <summary>The glyph of a character, or null when the font has none.</summary>
    public ushort? Glyph(int codePoint) => _glyphs.TryGetValue(codePoint, out var glyph) ? glyph : null;

    /// <summary>The advance of a glyph in thousandths of the font size, the unit of PDF widths.</summary>
    public int Width(ushort glyph) =>
        (int)Math.Round(_advances[Math.Min(glyph, _advances.Length - 1)] * 1000.0 / UnitsPerEm);

    /// <summary>A font unit measure in thousandths of the font size.</summary>
    public int Scale(int units) => (int)Math.Round(units * 1000.0 / UnitsPerEm);

    private static Dictionary<string, int> Tables(byte[] data)
    {
        var count = U16(data, 4);
        var tables = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var record = 12 + 16 * i;
            var tag = System.Text.Encoding.ASCII.GetString(data, record, 4);
            tables[tag] = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 8));
        }

        return tables;
    }

    /// <summary>
    /// Unicode to glyph, from the Windows Unicode subtable: format 12 (full
    /// range) when the font has it, format 4 (the basic plane) otherwise.
    /// </summary>
    private static Dictionary<int, ushort> Cmap(byte[] data, int cmap)
    {
        var count = U16(data, cmap + 2);
        int? format4 = null, format12 = null;

        for (var i = 0; i < count; i++)
        {
            var record = cmap + 4 + 8 * i;
            var platform = U16(data, record);
            var encoding = U16(data, record + 2);
            var offset = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(record + 4));
            var format = U16(data, offset);

            if (platform == 3 && encoding == 10 && format == 12)
            {
                format12 = offset;
            }
            else if (platform == 3 && encoding == 1 && format == 4)
            {
                format4 = offset;
            }
        }

        var glyphs = new Dictionary<int, ushort>();

        if (format12 is { } table12)
        {
            var groups = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(table12 + 12));
            for (var g = 0; g < groups; g++)
            {
                var group = table12 + 16 + 12 * g;
                var start = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(group));
                var end = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(group + 4));
                var first = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(group + 8));
                for (var c = start; c <= end; c++)
                {
                    glyphs[(int)c] = (ushort)(first + (c - start));
                }
            }

            return glyphs;
        }

        var table = format4 ?? throw new InvalidOperationException("The font has no Windows Unicode cmap.");
        var segments = U16(data, table + 6) / 2;
        var ends = table + 14;
        var starts = ends + 2 * segments + 2;
        var deltas = starts + 2 * segments;
        var ranges = deltas + 2 * segments;

        for (var s = 0; s < segments; s++)
        {
            var end = U16(data, ends + 2 * s);
            var start = U16(data, starts + 2 * s);
            var delta = S16(data, deltas + 2 * s);
            var rangeOffset = U16(data, ranges + 2 * s);

            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                ushort glyph;
                if (rangeOffset == 0)
                {
                    glyph = (ushort)(c + delta);
                }
                else
                {
                    var at = ranges + 2 * s + rangeOffset + 2 * (c - start);
                    glyph = U16(data, at);
                    if (glyph != 0)
                    {
                        glyph = (ushort)(glyph + delta);
                    }
                }

                if (glyph != 0)
                {
                    glyphs[c] = glyph;
                }
            }
        }

        return glyphs;
    }

    private static ushort U16(byte[] data, int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));

    private static short S16(byte[] data, int at) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at));
}
