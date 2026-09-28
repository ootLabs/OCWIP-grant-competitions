using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// A TrueType file cut down to the glyphs one document draws (T-45c). The
/// whole Noto Sans is about 600 KB, 300 KB compressed, in every PDF; a
/// contract uses a hundred or so of its thousands of glyphs.
///
/// The glyph numbers stay as they are: the outline of every glyph the
/// document does not use is emptied (a zero length entry in loca), instead
/// of renumbering. The PDF addresses glyphs by number (Identity-H with
/// /CIDToGIDMap /Identity), so the widths, the content streams and the
/// ToUnicode map need no change at all, and hmtx, maxp and cmap stay valid.
/// A compound glyph (an accented letter built from a base and an accent)
/// keeps its components too, and glyph 0, .notdef, is always kept.
///
/// Tables a PDF viewer never reads for a CID font (layout, kerning, colour
/// and variation tables) are left out, and post drops the glyph names
/// (format 3), which only help a font editor.
/// </summary>
internal static class TrueTypeSubset
{
    /// <summary>What a PDF viewer needs to draw TrueType outlines (ISO 32000-1, 9.9, plus the hinting tables).</summary>
    private static readonly HashSet<string> Kept = new(StringComparer.Ordinal)
    {
        "head", "hhea", "maxp", "hmtx", "loca", "glyf", "cmap", "cvt ", "fpgm", "prep", "OS/2", "post", "name",
    };

    private const ushort ArgsAreWords = 0x0001;
    private const ushort HaveScale = 0x0008;
    private const ushort MoreComponents = 0x0020;
    private const ushort HaveXAndYScale = 0x0040;
    private const ushort HaveTwoByTwo = 0x0080;

    public static byte[] Create(byte[] font, IEnumerable<ushort> glyphs)
    {
        var tables = Tables(font);
        var head = tables["head"];
        var glyphCount = U16(font, tables["maxp"].Offset + 4);
        var longLoca = S16(font, head.Offset + 50) == 1;
        var loca = tables["loca"].Offset;
        var glyf = tables["glyf"].Offset;

        (int Start, int End) Outline(int glyph) => longLoca
            ? ((int)U32(font, loca + 4 * glyph), (int)U32(font, loca + 4 * (glyph + 1)))
            : (2 * U16(font, loca + 2 * glyph), 2 * U16(font, loca + 2 * (glyph + 1)));

        var keep = Closure(font, glyf, glyphCount, Outline, glyphs);

        // The new glyf and a long loca: every kept outline in place, 4 byte
        // aligned, every other glyph of length zero.
        using var outlines = new MemoryStream();
        var offsets = new byte[4 * (glyphCount + 1)];
        for (var glyph = 0; glyph < glyphCount; glyph++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(offsets.AsSpan(4 * glyph), (uint)outlines.Length);
            var (start, end) = Outline(glyph);
            if (keep.Contains((ushort)glyph) && end > start)
            {
                outlines.Write(font, glyf + start, end - start);
                while (outlines.Length % 4 != 0)
                {
                    outlines.WriteByte(0);
                }
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(offsets.AsSpan(4 * glyphCount), (uint)outlines.Length);

        var written = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (tag, table) in tables)
        {
            if (Kept.Contains(tag))
            {
                written[tag] = font.AsSpan(table.Offset, table.Length).ToArray();
            }
        }

        written["glyf"] = outlines.ToArray();
        written["loca"] = offsets;

        var newHead = written["head"];
        BinaryPrimitives.WriteInt16BigEndian(newHead.AsSpan(50), 1);
        BinaryPrimitives.WriteUInt32BigEndian(newHead.AsSpan(8), 0);

        if (written.TryGetValue("post", out var post) && post.Length >= 32)
        {
            var names = post[..32];
            BinaryPrimitives.WriteUInt32BigEndian(names, 0x00030000);
            written["post"] = names;
        }

        var file = Assemble(U32(font, 0), written);

        // The whole file sums to 0xB1B0AFBA once head carries the adjustment.
        var headAt = HeadOffset(file);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(headAt + 8), unchecked(0xB1B0AFBA - Checksum(file)));
        return file;
    }

    /// <summary>
    /// "ABCDEF+": the tag ISO 32000-1 (9.6.4) asks a subset font's name to
    /// carry, so a viewer never takes two different subsets for one font.
    /// Derived from the glyphs, so the same document gets the same name.
    /// </summary>
    public static string Tag(IEnumerable<ushort> glyphs)
    {
        var bytes = glyphs.Order().SelectMany(BitConverter.GetBytes).ToArray();
        var hash = SHA256.HashData(bytes);
        return new string([.. hash.Take(6).Select(b => (char)('A' + b % 26))]) + "+";
    }

    private static HashSet<ushort> Closure(
        byte[] font, int glyf, int glyphCount, Func<int, (int Start, int End)> outline, IEnumerable<ushort> glyphs)
    {
        var keep = new HashSet<ushort>();
        var pending = new Stack<ushort>(glyphs.Where(glyph => glyph < glyphCount).Append((ushort)0));

        while (pending.TryPop(out var glyph))
        {
            if (!keep.Add(glyph))
            {
                continue;
            }

            var (start, end) = outline(glyph);
            if (end - start < 10 || S16(font, glyf + start) >= 0)
            {
                continue;
            }

            // A compound glyph: its components follow the 10 byte header.
            var at = glyf + start + 10;
            ushort flags;
            do
            {
                flags = U16(font, at);
                var component = U16(font, at + 2);
                if (component < glyphCount && !keep.Contains(component))
                {
                    pending.Push(component);
                }

                at += 4 + ((flags & ArgsAreWords) != 0 ? 4 : 2);
                at += (flags & HaveScale) != 0 ? 2 : (flags & HaveXAndYScale) != 0 ? 4 : (flags & HaveTwoByTwo) != 0 ? 8 : 0;
            }
            while ((flags & MoreComponents) != 0 && at < glyf + end);
        }

        return keep;
    }

    private static byte[] Assemble(uint sfntVersion, SortedDictionary<string, byte[]> tables)
    {
        var count = tables.Count;
        var power = 1;
        var selector = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            selector++;
        }

        var headerLength = 12 + 16 * count;
        var length = headerLength + tables.Values.Sum(table => (table.Length + 3) & ~3);
        var file = new byte[length];

        BinaryPrimitives.WriteUInt32BigEndian(file, sfntVersion);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(4), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(6), (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(8), (ushort)selector);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(10), (ushort)(count * 16 - power * 16));

        var record = 12;
        var offset = headerLength;
        foreach (var (tag, table) in tables)
        {
            Encoding.ASCII.GetBytes(tag, file.AsSpan(record));
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(record + 4), Checksum(table));
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(record + 8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(record + 12), (uint)table.Length);
            table.CopyTo(file, offset);
            offset += (table.Length + 3) & ~3;
            record += 16;
        }

        return file;
    }

    private static int HeadOffset(byte[] file)
    {
        var tables = Tables(file);
        return tables["head"].Offset;
    }

    private static Dictionary<string, (int Offset, int Length)> Tables(byte[] data)
    {
        var count = U16(data, 4);
        var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var record = 12 + 16 * i;
            tables[Encoding.ASCII.GetString(data, record, 4)] = ((int)U32(data, record + 8), (int)U32(data, record + 12));
        }

        return tables;
    }

    /// <summary>The TrueType checksum: the data as big endian 32 bit words, zero padded, summed.</summary>
    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i += 4)
        {
            var word = 0u;
            for (var b = 0; b < 4; b++)
            {
                word = (word << 8) | (i + b < data.Length ? data[i + b] : 0u);
            }

            sum = unchecked(sum + word);
        }

        return sum;
    }

    private static ushort U16(byte[] data, int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));

    private static short S16(byte[] data, int at) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at));

    private static uint U32(byte[] data, int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
}
