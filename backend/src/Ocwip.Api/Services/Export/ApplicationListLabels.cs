using System.Globalization;
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

    public static string Status(ApplicationStatus status) =>
        status switch
        {
            ApplicationStatus.Submitted => "Złożony",
            ApplicationStatus.Draft => "Wersja robocza",
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

    /// <summary>Digits only, the way CompetitionIntakeMessage writes dates.</summary>
    public static string Moment(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, WarsawOrUtc())
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Which clock <see cref="Moment"/> reads, for the export to say.</summary>
    public static string TimeLabel => IsPolishTime ? "czasu polskiego" : "czasu UTC";

    /// <summary>
    /// UTC when the image carries no time zone database, the fallback the
    /// intake message already takes (CompetitionIntake): an hour off would be
    /// worse than a clearly named UTC.
    /// </summary>
    private static bool IsPolishTime => WarsawOrUtc() != TimeZoneInfo.Utc;

    private static TimeZoneInfo WarsawOrUtc() =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Warsaw", out var zone)
            ? zone
            : TimeZoneInfo.Utc;
}
