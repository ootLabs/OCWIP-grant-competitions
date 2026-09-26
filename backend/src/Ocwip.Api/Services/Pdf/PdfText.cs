using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// Text made safe for SimplePdfDocument, which only writes ASCII (Base14
/// fonts have no Polish diacritics without an embedded font). Shared by the
/// confirmation slip (T-33), the list of applications (T-35), the ranking
/// list (T-42a) and the whole application (T-44), so all of them drop a
/// character the same way.
/// </summary>
internal static class PdfText
{
    /// <summary>Polish diacritics only; everything else outside printable
    /// ASCII becomes "?" rather than silently vanishing, so a title nobody
    /// proofread for this document at least shows that something was
    /// dropped.</summary>
    private static readonly Dictionary<char, char> Diacritics = new()
    {
        ['ą'] = 'a', ['ć'] = 'c', ['ę'] = 'e', ['ł'] = 'l', ['ń'] = 'n',
        ['ó'] = 'o', ['ś'] = 's', ['ź'] = 'z', ['ż'] = 'z',
        ['Ą'] = 'A', ['Ć'] = 'C', ['Ę'] = 'E', ['Ł'] = 'L', ['Ń'] = 'N',
        ['Ó'] = 'O', ['Ś'] = 'S', ['Ź'] = 'Z', ['Ż'] = 'Z',
    };

    /// <summary>
    /// The typography a word processor puts into pasted text (T-44): Polish
    /// quotation marks, dashes, an ellipsis, a hard space, a tab. Each has a
    /// plain ASCII reading, and printing "?" for them would fill an
    /// application's paper copy with question marks. Written as escapes:
    /// the dashes themselves are banned from the source (check_text.py).
    /// </summary>
    private static readonly Dictionary<char, string> Typography = new()
    {
        ['\u201E'] = "\"", ['\u201D'] = "\"", ['\u201C'] = "\"", ['\u00AB'] = "\"", ['\u00BB'] = "\"",
        ['\u2018'] = "'", ['\u2019'] = "'", ['\u201A'] = "'",
        ['\u2013'] = "-", ['\u2014'] = "-", ['\u2212'] = "-",
        ['\u2026'] = "...", ['\u00A0'] = " ", ['\u202F'] = " ", ['\t'] = "    ",
    };

    public static string Transliterate(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (character is >= (char)0x20 and <= (char)0x7E)
            {
                builder.Append(character);
            }
            else if (Diacritics.TryGetValue(character, out var replacement))
            {
                builder.Append(replacement);
            }
            else if (Typography.TryGetValue(character, out var plain))
            {
                builder.Append(plain);
            }
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }
}
