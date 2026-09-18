using System.Globalization;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The intake cut off as the caller sees it (T-21).
///
/// Sent with every competition, on both the operator route and the public one,
/// so that a screen draws the "Wypelnij wniosek" button and the countdown from
/// the rule instead of comparing the two dates next to it a second time. The
/// card is explicit that the front shows what this rule returns.
/// </summary>
/// <param name="AcceptsApplications">
/// The one flag anything guarding a write should look at.
/// </param>
/// <param name="ClosesAt">
/// The closing moment in UTC, or null for a continuous intake. A countdown is
/// drawn from this, which is why it travels as an instant and not as text.
/// </param>
/// <param name="Message">
/// Why, in Polish, with the boundary value in it. D12: a message carries the
/// value that decided it, so it names the closing date and hour rather than
/// repeating that the intake is closed.
/// </param>
public sealed record CompetitionIntakeResponse(
    bool AcceptsApplications,
    IntakeState State,
    DateTimeOffset OpensAt,
    DateTimeOffset? ClosesAt,
    string Message)
{
    public static CompetitionIntakeResponse From(CompetitionIntakeState intake) =>
        new(
            intake.AcceptsApplications,
            intake.State,
            intake.OpensAt,
            intake.ClosesAt,
            CompetitionIntakeMessage.For(intake));
}

/// <summary>
/// The Polish wording of the cut off, with the moment that decided it spelled
/// out in the time the reader lives in.
///
/// The system serves one region, so "local time" is Polish time, resolved
/// here, at the edge, and nowhere else. The stored instants stay UTC, which is
/// what makes the October switch a formatting question rather than a data one:
/// the same wall clock hour on either side of it is two different instants,
/// and both are named by their own hour.
/// </summary>
public static class CompetitionIntakeMessage
{
    private const string PolishTimeZoneId = "Europe/Warsaw";

    /// <summary>
    /// Digits rather than month names on purpose: no culture data has to be
    /// present in the image for a date to come out readable, and a competition
    /// closing "25.10.2026" cannot be misread the way a localised month can.
    /// </summary>
    private const string DateFormat = "dd.MM.yyyy";

    private const string TimeFormat = "HH:mm";

    public static string For(CompetitionIntakeState intake) =>
        For(intake, PolishTimeZone());

    /// <summary>
    /// The same wording against a chosen time zone. Null means the image has
    /// no time zone database, which is the branch the fallback exists for and
    /// the only way a test can reach it.
    /// </summary>
    internal static string For(
        CompetitionIntakeState intake,
        TimeZoneInfo? zone) => intake.State switch
    {
        IntakeState.Open when intake.ClosesAt is null =>
            "Nabór ciągły. Wnioski można składać bez terminu końcowego.",

        IntakeState.Open =>
            "Nabór trwa. Wnioski można składać do "
            + Moment(intake.ClosesAt!.Value, zone)
            + ".",

        IntakeState.NotYetOpen =>
            "Nabór jeszcze się nie rozpoczął. Wnioski można składać od "
            + Moment(intake.OpensAt, zone)
            + ".",

        // A continuous intake closed by hand has no moment to name, and an
        // invented one would be worse than none.
        IntakeState.Closed when intake.ClosesAt is null =>
            "Nabór został zamknięty. Wniosku nie można już złożyć.",

        IntakeState.Closed =>
            "Nabór został zamknięty "
            + Moment(intake.ClosesAt!.Value, zone)
            + ". Wniosku nie można już złożyć.",

        IntakeState.Unavailable => "Ten konkurs nie przyjmuje wniosków.",

        // Not a default sentence: a state added to the enum without a wording
        // here would otherwise be answered with the most generic one there is,
        // silently and in the place the card asks for an unambiguous error.
        _ => throw new ArgumentOutOfRangeException(
            nameof(intake),
            intake.State,
            "No wording for this intake state."),
    };

    private static string Moment(DateTimeOffset instant, TimeZoneInfo? zone)
    {
        var (local, label) = ToReaderTime(instant, zone);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{local.ToString(DateFormat, CultureInfo.InvariantCulture)} "
            + $"o godzinie {local.ToString(TimeFormat, CultureInfo.InvariantCulture)} "
            + $"{label}");
    }

    /// <summary>
    /// The instant as a Polish wall clock, or as UTC when the image has no
    /// time zone database.
    ///
    /// The fallback says UTC out loud instead of printing a Polish sentence
    /// around an hour that is one or two off. A wrong hour in a deadline
    /// message is the one thing this card exists to prevent.
    /// </summary>
    private static (DateTimeOffset Local, string Label) ToReaderTime(
        DateTimeOffset instant,
        TimeZoneInfo? zone) =>
        zone is null
            ? (instant.ToUniversalTime(), "czasu UTC")
            : (TimeZoneInfo.ConvertTime(instant, zone), "czasu polskiego");

    private static TimeZoneInfo? PolishTimeZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(PolishTimeZoneId, out var zone)
            ? zone
            : null;
}
