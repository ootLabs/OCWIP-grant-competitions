using System.Globalization;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Export;

/// <summary>
/// The Polish words the exported list of applications uses (T-35), the same
/// ones the screen shows (frontend/lib/operator-applications.ts), so the
/// spreadsheet an operator sends on reads like the screen they saw.
/// </summary>
internal static class ApplicationListLabels
{
    public static readonly IReadOnlyList<string> Columns =
    [
        "Lp.",
        "Numer wniosku",
        "Nazwa podmiotu",
        "Rodzaj wnioskodawcy",
        "Tytuł projektu",
        "Całkowity koszt zadania",
        "Wnioskowana kwota",
        "Status",
        "Ocena formalna",
        "Data złożenia",
    ];

    // docs/reguly-biznesowe.md, "Typy podmiotów".
    public static string EntityType(EntityType type) =>
        type switch
        {
            Models.EntityType.InformalGroup => "Grupa nieformalna",
            Models.EntityType.PatronInformalGroup => "Grupa nieformalna pod patronatem",
            Models.EntityType.Organisation => "Organizacja",
            _ => throw new InvalidOperationException($"Unlabelled entity type: {type}"),
        };

    /// <summary>The same words as formalLabels in frontend/lib/operator-evaluation.ts.</summary>
    public static string Formal(FormalStanding standing) =>
        standing switch
        {
            FormalStanding.NotStarted => "Nierozpoczęta",
            FormalStanding.InProgress => "W toku",
            FormalStanding.Passed => "Pozytywna",
            FormalStanding.Failed => "Negatywna",
            _ => throw new InvalidOperationException($"Unlabelled formal standing: {standing}"),
        };

    public static string Status(ApplicationStatus status) =>
        status switch
        {
            ApplicationStatus.Submitted => "Złożony",
            ApplicationStatus.Draft => "Wersja robocza",
            ApplicationStatus.Funded => "Dofinansowany, umowa niepodpisana",
            ApplicationStatus.Reserve => "Lista rezerwowa",
            ApplicationStatus.Rejected => "Odrzucony",
            ApplicationStatus.ContractSigned => "Umowa podpisana",
            ApplicationStatus.Settled => "Rozliczony",
            ApplicationStatus.Returned => "Zwrócony do poprawy",
            ApplicationStatus.Resigned => "Rezygnacja",
            _ => throw new InvalidOperationException($"Unlabelled status: {status}"),
        };

    /// <summary>
    /// Two decimals, printed only (D13): stored amounts keep four. A decimal
    /// comma and no grouping, so a Polish spreadsheet reads it as a number.
    /// By hand rather than through the "pl-PL" culture, for the reason
    /// PolishNumbers gives: an image without ICU data formats quietly in the
    /// invariant culture instead of failing.
    /// </summary>
    public static string Amount(decimal? amount) =>
        amount is { } value
            ? Math.Round(value, 2, MidpointRounding.AwayFromZero)
                .ToString("0.00", CultureInfo.InvariantCulture)
                .Replace('.', ',')
            : string.Empty;

    /// <summary>Digits only, on the reader's clock (ReaderTime).</summary>
    public static string Moment(DateTimeOffset moment) => ReaderTime.Moment(moment);

    /// <summary>The calendar day of a moment on the same clock as <see cref="Moment"/>.</summary>
    public static DateOnly Day(DateTimeOffset moment) => ReaderTime.Day(moment);

    /// <summary>Which clock <see cref="Moment"/> reads, for the export to say.</summary>
    public static string TimeLabel => ReaderTime.Label;
}
