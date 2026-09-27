using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// Text made ready for a line of SimplePdfDocument. Polish letters and the
/// typography a word processor pastes (quotation marks, dashes) stay as they
/// are since T-45a, because the embedded font draws them; what goes is only
/// what a single text line cannot show: a tab becomes spaces, other control
/// characters are dropped.
/// </summary>
internal static class PdfText
{
    public static string Printable(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (character == '\t')
            {
                builder.Append("    ");
            }
            else if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
