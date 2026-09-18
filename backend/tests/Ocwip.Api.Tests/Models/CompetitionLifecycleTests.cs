using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Models;

/// <summary>
/// The effective state: what the stored column says plus whatever the clock
/// has done since (T-20).
///
/// The boundary cases here are the ones D7 cares about, and T-21 will build
/// the "still taking applications" rule on top of this rather than repeating
/// the arithmetic, so the minute of closing is asserted from both sides.
/// </summary>
public sealed class CompetitionLifecycleTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset End =
        new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static Competition Competition(
        CompetitionStatus status = CompetitionStatus.Published,
        bool continuous = false) =>
        new()
        {
            Number = "1/2026",
            Title = "Konkurs testowy",
            Status = status,
            StartDate = Start,
            EndDate = continuous ? null : End,
            IsContinuousIntake = continuous,
            MaxGrantAmount = 5000m,
        };

    [Fact]
    public void ADraft_ShouldStayADraftHoweverLongItSitsThere()
    {
        // Arrange
        var competition = Competition(CompetitionStatus.Draft);

        // Act
        var status = CompetitionLifecycle.Effective(
            competition, End.AddYears(1));

        // Assert
        // Both dates are long past, and the competition was never published.
        // The clock does not publish anything: only an operator does, and
        // T-22 makes them confirm it.
        Assert.Equal(CompetitionStatus.Draft, status);
    }

    [Fact]
    public void APublishedCompetition_ShouldNotTakeApplicationsBeforeItsStart()
    {
        // Act
        var status = CompetitionLifecycle.Effective(
            Competition(), Start.AddMinutes(-1));

        // Assert
        // Visible to a guest, but "Wypelnij wniosek" does nothing yet. These
        // are two different moments and the report says so.
        Assert.Equal(CompetitionStatus.Published, status);
    }

    [Fact]
    public void TheIntake_ShouldOpenInTheMinuteItSaysItDoes()
    {
        // Act
        var status = CompetitionLifecycle.Effective(Competition(), Start);

        // Assert
        Assert.Equal(CompetitionStatus.OpenForApplications, status);
    }

    [Theory]
    [InlineData(-1, CompetitionStatus.OpenForApplications)]
    [InlineData(0, CompetitionStatus.Closed)]
    [InlineData(1, CompetitionStatus.Closed)]
    public void TheIntake_ShouldCloseAtTheClosingMinuteAndNotAMinuteLater(
        int minutesFromClosing,
        CompetitionStatus expected)
    {
        // Act
        var status = CompetitionLifecycle.Effective(
            Competition(), End.AddMinutes(minutesFromClosing));

        // Assert
        // D7: whoever arrives at 12:05 for a competition closing at 12:00 does
        // not file. 12:00:00 itself is already closed, which is why both dates
        // are truncated to a whole minute on the way in.
        Assert.Equal(expected, status);
    }

    [Fact]
    public void AContinuousIntake_ShouldNeverCloseOnItsOwn()
    {
        // Act
        var status = CompetitionLifecycle.Effective(
            Competition(continuous: true), Start.AddYears(5));

        // Assert
        // The trap T-21 is warned about: an empty closing date read as a date
        // in the past turns the one competition that never closes into one
        // that closed before it opened.
        Assert.Equal(CompetitionStatus.OpenForApplications, status);
    }

    [Fact]
    public void ACompetitionPublishedAfterBothItsDates_ShouldBeClosedAtOnce()
    {
        // Act
        var status = CompetitionLifecycle.Effective(
            Competition(), End.AddDays(1));

        // Assert
        // Two scheduled steps in one instant, which is why the derivation is a
        // loop. A single step would answer "trwa nabor" for a competition that
        // cannot be filed against, and the front would draw a working button.
        Assert.Equal(CompetitionStatus.Closed, status);
    }

    [Theory]
    [InlineData(CompetitionStatus.Closed)]
    [InlineData(CompetitionStatus.UnderReview)]
    [InlineData(CompetitionStatus.Resolved)]
    [InlineData(CompetitionStatus.Archived)]
    public void PastTheIntake_TheClockShouldHaveNothingLeftToSay(
        CompetitionStatus stored)
    {
        // Act
        var status = CompetitionLifecycle.Effective(
            Competition(stored), End.AddYears(10));

        // Assert
        // Everything after the intake window is somebody's decision, so the
        // stored value is the whole answer.
        Assert.Equal(stored, status);
    }

    [Fact]
    public void OnlyADraft_ShouldLackAPublicAddress()
    {
        // Act
        var hidden = Enum.GetValues<CompetitionStatus>()
            .Where(status => !CompetitionLifecycle.IsPubliclyVisible(status))
            .ToList();

        // Assert
        // Archived is deliberately not on this list: an archived competition
        // stays readable, because the public results archive (R-14) is the
        // whole point of keeping it.
        Assert.Equal([CompetitionStatus.Draft], hidden);
    }
}
