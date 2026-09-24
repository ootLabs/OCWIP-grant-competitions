using System.Globalization;
using System.Text;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// The confirmation slip an applicant downloads after submitting (T-33):
/// application number, competition, the form version the submission was made
/// against, the submission timestamp and the checksum, the same "informacje
/// techniczne" block the on screen application carries (D15, R-13).
///
/// Renders through SimplePdfDocument, which only accepts ASCII (Base14 fonts
/// have no Polish diacritics without an embedded font, see that class). Every
/// caller supplied string is transliterated here before it reaches the
/// writer; the labels below are typed without diacritics for the same
/// reason. This is a narrowing of THIS document only: everywhere else in the
/// product, UI text and stored data stay proper Polish per AGENTS.md.
/// </summary>
internal static class ApplicationConfirmationPdfBuilder
{
    public static byte[] Build(
        string applicationNumber,
        string competitionTitle,
        int formDefinitionVersionNumber,
        DateTimeOffset submittedAt,
        string checksum)
    {
        var lines = new[]
        {
            "Potwierdzenie zlozenia oferty",
            string.Empty,
            $"Numer wniosku: {applicationNumber}",
            $"Konkurs: {Transliterate(competitionTitle)}",
            $"Wersja formularza: {formDefinitionVersionNumber.ToString(CultureInfo.InvariantCulture)}",
            $"Data zlozenia (UTC): {submittedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
            $"Suma kontrolna: {checksum}",
            string.Empty,
            "Ten dokument potwierdza zlozenie oferty w wyznaczonym terminie.",
            "Wygenerowano automatycznie, nie wymaga podpisu.",
        };

        return SimplePdfDocument.Create(lines);
    }

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

    private static string Transliterate(string text)
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
