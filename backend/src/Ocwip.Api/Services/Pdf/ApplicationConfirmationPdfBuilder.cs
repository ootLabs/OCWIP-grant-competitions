using System.Globalization;

namespace Ocwip.Api.Services.Pdf;

/// <summary>
/// The confirmation slip an applicant downloads after submitting (T-33):
/// application number, competition, the form version the submission was made
/// against, the submission timestamp and the checksum, the same "informacje
/// techniczne" block the on screen application carries (D15, R-13).
///
/// Renders through SimplePdfDocument, which embeds a font with Polish
/// letters (T-45a).
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
            "Potwierdzenie złożenia oferty",
            string.Empty,
            $"Numer wniosku: {applicationNumber}",
            $"Konkurs: {PdfText.Printable(competitionTitle)}",
            $"Wersja formularza: {formDefinitionVersionNumber.ToString(CultureInfo.InvariantCulture)}",
            $"Data złożenia (UTC): {submittedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
            $"Suma kontrolna: {checksum}",
            string.Empty,
            "Ten dokument potwierdza złożenie oferty w wyznaczonym terminie.",
            "Wygenerowano automatycznie, nie wymaga podpisu.",
        };

        return SimplePdfDocument.Create(lines);
    }
}
