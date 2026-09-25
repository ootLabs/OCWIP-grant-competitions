using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// Text made safe for SimplePdfDocument, which only writes ASCII (Base14
/// fonts have no Polish diacritics without an embedded font). Shared by the
/// confirmation slip (T-33) and the list of applications (T-35), so both
/// drop a character the same way.
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
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }
}
