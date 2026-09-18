using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Models;

/// <summary>
/// The one rule that answers "does this competition still take applications"
/// (T-21, decision D7).
///
/// The boundary tests here are the card's checklist: a minute before, the
/// closing minute itself, a minute after. They are asserted through this rule
/// and not through CompetitionLifecycle, because the point of the card is that
/// this is the seam every write path asks, so this is the seam that has to be
/// right.
/// </summary>
public sealed class CompetitionIntakeTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset End =
        new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static Competition Competition(
        CompetitionStatus status = CompetitionStatus.Published,
        bool continuous = false,
        bool active = true) =>
        new()
        {
            Number = "1/2026",
            Title = "Konkurs testowy",
            Status = status,
            StartDate = Start,
            EndDate = continuous ? null : End,
            IsContinuousIntake = continuous,
            MaxGrantAmount = 5000m,
            IsActive = active,
            DeactivatedAt = active ? null : Start,
        };

    [Theory]
    [InlineData(-1, IntakeState.Open)]
    [InlineData(0, IntakeState.Closed)]
    [InlineData(1, IntakeState.Closed)]
    public void TheIntake_ShouldStopInTheClosingMinuteItself(
        int minutesFromClosing,
        IntakeState expected)
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(), End.AddMinutes(minutesFromClosing));

        // Assert
        // D7 in one assertion: 11:59 files, 12:00 does not, 12:01 does not.
        // Whoever walks in at 12:05 is on the same side of this line as 12:00.
        Assert.Equal(expected, intake.State);
        Assert.Equal(expected is IntakeState.Open, intake.AcceptsApplications);
    }

    [Fact]
    public void TheClosingMoment_ShouldTravelWithTheAnswer()
    {
        // Act
        var intake = CompetitionIntake.For(Competition(), End.AddMinutes(1));

        // Assert
        // D12: whatever refuses the application has the value that decided it
        // to hand, so it can name the deadline instead of quoting the rule.
        Assert.Equal(End, intake.ClosesAt);
        Assert.Equal(Start, intake.OpensAt);
    }

    [Fact]
    public void AnAnnouncedCompetition_ShouldNotTakeAnythingBeforeItsStart()
    {
        // Act
        var intake = CompetitionIntake.For(Competition(), Start.AddMinutes(-1));

        // Assert
        // Visible and not fileable are two different states, and Closed is the
        // wrong one to reuse: this intake has not happened yet.
        Assert.Equal(IntakeState.NotYetOpen, intake.State);
        Assert.False(intake.AcceptsApplications);
    }

    [Fact]
    public void AContinuousIntake_ShouldStayOpenWithNoClosingMoment()
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(continuous: true), Start.AddYears(5));

        // Assert
        // The trap the card names: an empty closing date read as a date in the
        // past would answer "nabor zamkniety" for the one competition that
        // never closes.
        Assert.Equal(IntakeState.Open, intake.State);
        Assert.True(intake.AcceptsApplications);
        Assert.Null(intake.ClosesAt);
    }

    [Fact]
    public void ADraft_ShouldHaveNoIntakeRatherThanAClosedOne()
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(CompetitionStatus.Draft), End.AddYears(1));

        // Assert
        // Unavailable, not Closed: a draft has no public address at all, so
        // there is no intake that ended and no date worth naming.
        Assert.Equal(IntakeState.Unavailable, intake.State);
        Assert.False(intake.AcceptsApplications);
    }

    [Fact]
    public void AnInactiveCompetition_ShouldTakeNothingWhateverItsDatesSay()
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(active: false), Start.AddMinutes(1));

        // Assert
        // Its window is wide open and its row is kept only for the retention
        // period. Asking the clock first would answer "trwa nabor" for
        // something no applicant can reach.
        Assert.Equal(IntakeState.Unavailable, intake.State);
        Assert.False(intake.AcceptsApplications);
    }

    [Theory]
    [InlineData(CompetitionStatus.Closed)]
    [InlineData(CompetitionStatus.UnderReview)]
    [InlineData(CompetitionStatus.Resolved)]
    [InlineData(CompetitionStatus.Archived)]
    public void PastTheIntake_EveryStateShouldRefuse(CompetitionStatus stored)
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(stored), Start.AddMinutes(1));

        // Assert
        // Inside the window by the calendar, and still refusing: an operator
        // who moved the competition on has ended the intake by doing so.
        Assert.Equal(IntakeState.Closed, intake.State);
        Assert.False(intake.AcceptsApplications);
    }

    [Theory]

    // Noon in Warsaw before the October switch is 10:00 UTC, and after it is
    // 11:00 UTC. Both are entered as noon by an operator who never thinks
    // about offsets.
    [InlineData("2026-10-20T12:00:00+02:00", "2026-10-20T09:59:00Z", true)]
    [InlineData("2026-10-20T12:00:00+02:00", "2026-10-20T10:00:00Z", false)]
    [InlineData("2026-11-03T12:00:00+01:00", "2026-11-03T10:59:00Z", true)]
    [InlineData("2026-11-03T12:00:00+01:00", "2026-11-03T11:00:00Z", false)]
    public void TheCutOff_ShouldSurviveTheSwitchToWinterTime(
        string closing,
        string now,
        bool expected)
    {
        // Arrange
        var competition = Competition();
        competition.EndDate = DateTimeOffset.Parse(closing);

        // Act
        var intake = CompetitionIntake.For(
            competition, DateTimeOffset.Parse(now));

        // Assert
        // The switch lands in the middle of the competition season, and this
        // is what it must not do: move the cut off by an hour. It cannot,
        // because nothing here compares wall clocks. Both instants are UTC and
        // the local hour is a thing the edge prints.
        Assert.Equal(expected, intake.AcceptsApplications);
    }

    [Fact]
    public void AnIntakeClosedByHand_ShouldNotNameADeadlineStillInTheFuture()
    {
        // Arrange
        // Closed by an operator twenty days before its own date, which the
        // transition table allows.
        var competition = Competition(CompetitionStatus.Closed);

        // Act
        var intake = CompetitionIntake.For(competition, End.AddDays(-20));

        // Assert
        // The date in the column is no longer the moment anything happened.
        // Sent on, it would tell an applicant that the intake "closed" on a
        // day that has not arrived, and hand T-23 a countdown to run down to
        // on a competition that is already shut.
        Assert.Equal(IntakeState.Closed, intake.State);
        Assert.Null(intake.ClosesAt);
    }

    [Fact]
    public void AnIntakeClosedByItsOwnDate_ShouldStillNameIt()
    {
        // Act
        var intake = CompetitionIntake.For(
            Competition(), End.AddMinutes(1));

        // Assert
        // The other side of the same rule: here the date is exactly what
        // closed the intake, so it is what the refusal has to quote.
        Assert.Equal(End, intake.ClosesAt);
    }

    [Fact]
    public void TheDefaultState_ShouldRefuse()
    {
        // Act
        var state = default(IntakeState);

        // Assert
        // A value nobody set must not come out accepting applications:
        // AGENTS.md rule 1, applied to the enum rather than to a route.
        Assert.Equal(IntakeState.Unavailable, state);
        Assert.False(
            new CompetitionIntakeState(state, Start, End).AcceptsApplications);
    }

    [Fact]
    public void OnlyOneState_ShouldEverAcceptApplications()
    {
        // Act
        var accepting = Enum.GetValues<IntakeState>()
            .Where(state =>
                new CompetitionIntakeState(state, Start, End)
                    .AcceptsApplications)
            .ToList();

        // Assert
        // A state added later defaults to refusing, which is AGENTS.md rule 1
        // applied to the clock: no rule means no access.
        Assert.Equal([IntakeState.Open], accepting);
    }
}
