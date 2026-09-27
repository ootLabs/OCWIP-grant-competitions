using System.Text;
using System.Text.RegularExpressions;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// The text of a PDF from SimplePdfDocument, read the way a PDF reader copies
/// it (T-45a): glyph numbers from the content streams, turned back into text
/// through the ToUnicode map. One line per text line, pages in order.
/// </summary>
internal static partial class PdfTextReader
{
    public static string Text(byte[] pdf)
    {
        var raw = Encoding.Latin1.GetString(pdf);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match entry in BfChar().Matches(raw))
        {
            map[entry.Groups[1].Value] = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(entry.Groups[2].Value));
        }

        var lines = TextShow().Matches(raw).Select(show =>
        {
            var hex = show.Groups[1].Value;
            var builder = new StringBuilder();
            for (var i = 0; i + 4 <= hex.Length; i += 4)
            {
                builder.Append(map.TryGetValue(hex.Substring(i, 4), out var text) ? text : "�");
            }

            return builder.ToString();
        });

        return string.Join('\n', lines);
    }

    /// <summary>How many pages the document says it has.</summary>
    public static int Pages(byte[] pdf) =>
        int.Parse(PageCount().Match(Encoding.Latin1.GetString(pdf)).Groups[1].Value);

    [GeneratedRegex(@"<([0-9A-F]{4})> <([0-9A-F]+)>")]
    private static partial Regex BfChar();

    [GeneratedRegex(@"<([0-9A-F]*)> Tj")]
    private static partial Regex TextShow();

    [GeneratedRegex(@"/Type /Pages /Kids \[[^\]]*\] /Count (\d+)")]
    private static partial Regex PageCount();
}
