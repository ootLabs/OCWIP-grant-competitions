using System.Globalization;

namespace Ocwip.Api.Services;

/// <summary>
/// An instant on the clock the reader keeps, for anything the product hands a
/// person: mail, a PDF, an export.
///
/// The product says "czasu polskiego" everywhere it names an hour, because a
/// deadline is a Polish wall clock. Two places printed UTC instead, the
/// confirmation mail and the confirmation PDF, so somebody who submitted at
/// 16:24 on the screen held a confirmation saying 14:24. Two hours is nothing
/// until a submission lands near midnight on the closing day, and then the
/// paper proof of being in time names the wrong date.
///
/// UTC stays as the fallback when the image carries no time zone database,
/// named out loud, which is the choice CompetitionIntakeMessage already made:
/// an hour quietly one or two off is worse than a clearly labelled UTC.
/// </summary>
internal static class ReaderTime
{
    /// <summary>Digits only: yyyy-MM-dd HH:mm, the way the exports write a moment.</summary>
    public static string Moment(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, Zone)
            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The calendar day of a moment on the same clock as <see cref="Moment"/>.</summary>
    public static DateOnly Day(DateTimeOffset moment) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, Zone).DateTime);

    /// <summary>Which clock <see cref="Moment"/> reads, for the text to say.</summary>
    public static string Label => Zone != TimeZoneInfo.Utc ? "czasu polskiego" : "czasu UTC";

    /// <summary>
    /// Looked up once, not once per row: the image does not grow a time zone
    /// database while it runs.
    /// </summary>
    private static readonly TimeZoneInfo Zone = WarsawOrUtc();

    private static TimeZoneInfo WarsawOrUtc() =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Warsaw", out var zone)
            ? zone
            : TimeZoneInfo.Utc;
}
