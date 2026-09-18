using System.Text.Json.Serialization;

namespace Ocwip.Api.Models;

/// <summary>
/// Whether a competition is taking applications, as one value rather than as a
/// boolean plus whatever the caller guessed about why (T-21).
///
/// Text on the wire, by attribute rather than by a serializer option, for the
/// reason written out on Models/Role.cs: a number crossing the wire is a
/// number the front has to keep a second table for, and the two tables are
/// what drift once a value is added.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntakeState>))]
public enum IntakeState
{
    /// <summary>
    /// Applications are being taken right now.
    /// </summary>
    Open,

    /// <summary>
    /// Announced, readable, and not taking anything yet. Being visible and
    /// being fileable are two different moments, and the report is explicit
    /// that the publication date is a separate field from the intake start.
    /// </summary>
    NotYetOpen,

    /// <summary>
    /// The intake was open and the closing moment has passed. D7 lives here:
    /// whoever arrives at 12:05 for a competition closing at 12:00 is in this
    /// state, not in a grace period.
    /// </summary>
    Closed,

    /// <summary>
    /// This competition never takes applications through this address: it is a
    /// draft, or it has been marked inactive. Deliberately not Closed, because
    /// "the intake ended" is a different sentence from "there is no intake",
    /// and only the first one has a date in it.
    /// </summary>
    Unavailable
}

/// <param name="OpensAt">
/// When the intake starts. Always known, because the start date is required.
/// </param>
/// <param name="ClosesAt">
/// When the intake ends, or null for a continuous intake, which is the case
/// this rule exists to keep separate from "the date has passed".
/// </param>
public sealed record CompetitionIntakeState(
    IntakeState State,
    DateTimeOffset OpensAt,
    DateTimeOffset? ClosesAt)
{
    /// <summary>
    /// The one question every path is meant to ask instead of comparing dates
    /// of its own.
    /// </summary>
    public bool AcceptsApplications => State is IntakeState.Open;
}

/// <summary>
/// The single answer to "does this competition still take applications"
/// (T-21, decision D7).
///
/// One place, and the card says why: the same comparison written into
/// submission, into autosave and into attachment upload is three copies that
/// drift apart at the first change, and the minute they drift in is the
/// closing minute.
///
/// Built ON TOP of CompetitionLifecycle rather than next to it. The lifecycle
/// already derives the state from the clock, already truncates both dates to a
/// whole minute, and already knows that a continuous intake has no closing
/// moment. Repeating any of that here would be a second rule pretending to be
/// the first one.
///
/// Everything is UTC. Turning the closing moment into a wall clock time is a
/// job for the edge, which is why this type carries instants and not strings.
/// </summary>
public static class CompetitionIntake
{
    public static CompetitionIntakeState For(
        Competition competition,
        DateTimeOffset now)
    {
        // A continuous intake has no closing moment at all. Reading the empty
        // column as a date in the past is the exact trap the card names: it
        // turns the one competition that never closes into one that closed
        // before it opened.
        var closesAt = competition.IsContinuousIntake
            ? null
            : competition.EndDate;

        return new CompetitionIntakeState(
            StateOf(competition, now),
            competition.StartDate,
            closesAt);
    }

    private static IntakeState StateOf(
        Competition competition,
        DateTimeOffset now)
    {
        // An inactive competition is kept for the retention period, not to
        // carry on being worked on. It is checked before the clock, because
        // its dates keep running and would otherwise answer "trwa nabor" for
        // something no applicant can reach.
        if (!competition.IsActive)
        {
            return IntakeState.Unavailable;
        }

        return CompetitionLifecycle.Effective(competition, now) switch
        {
            CompetitionStatus.OpenForApplications => IntakeState.Open,

            // Published and still published means the clock has not reached
            // the start date: the lifecycle would have moved it otherwise.
            CompetitionStatus.Published => IntakeState.NotYetOpen,

            CompetitionStatus.Draft => IntakeState.Unavailable,

            // Closed, under review, resolved, archived. Each of them is past
            // an intake that really happened, so each of them has a closing
            // moment to name.
            _ => IntakeState.Closed,
        };
    }
}
