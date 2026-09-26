using Ocwip.Api.Models;
using Ocwip.Api.Services.Export;

namespace Ocwip.Api.Services.Ranking;

/// <summary>
/// The text of one result mail (T-43): OCWIP's own words for the result, or a
/// default sentence, and a system footer with what the mail is about. Nothing
/// about any other applicant: no ranking, no other scores, no names.
/// </summary>
internal static class ResultEmail
{
    public static EmailMessage Compose(
        string to, Competition competition, ApplicationStatus result, string? number, decimal? awardedGrant)
    {
        var (subject, custom, fallback, label) = result switch
        {
            ApplicationStatus.Funded => (
                "Wynik konkursu: wniosek dofinansowany",
                competition.ResultEmailFunded,
                "Z przyjemnością informujemy, że Twój wniosek otrzymał dofinansowanie.",
                "dofinansowany"),
            ApplicationStatus.Reserve => (
                "Wynik konkursu: wniosek na liście rezerwowej",
                competition.ResultEmailReserve,
                "Twój wniosek został oceniony pozytywnie i trafił na listę rezerwową.",
                "lista rezerwowa"),
            ApplicationStatus.Rejected => (
                "Wynik konkursu: wniosek nie otrzymał dofinansowania",
                competition.ResultEmailRejected,
                "Informujemy, że Twój wniosek nie otrzymał dofinansowania.",
                "odrzucony"),
            _ => throw new InvalidOperationException($"No result mail for status {result}."),
        };

        var grant = result == ApplicationStatus.Funded && awardedGrant is { } amount
            ? $"\nPrzyznana kwota: {ApplicationListLabels.Amount(amount)} zł"
            : string.Empty;

        var body = $"""
            {(string.IsNullOrWhiteSpace(custom) ? fallback : custom.Trim())}

            Numer wniosku: {number}
            Konkurs: {competition.Title}
            Wynik: {label}{grant}

            Szczegóły, a po udostępnieniu także karty oceny, znajdziesz w swoim panelu.
            """;

        return new EmailMessage(to, subject, body);
    }
}
