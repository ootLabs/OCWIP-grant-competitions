using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Contracts;

/// <summary>
/// What the refusal says (T-21, decision D12).
///
/// The rule decides, this decides what the person reads, and the difference
/// that matters is the hour: the instants are UTC everywhere inside, and the
/// one place they turn into a wall clock is here. The October switch is
/// therefore a formatting test, and it is the one the card calls mandatory.
/// </summary>
public sealed class CompetitionIntakeMessageTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static CompetitionIntakeState Intake(
        IntakeState state,
        DateTimeOffset? closesAt) =>
        new(state, Start, closesAt);

    [Fact]
    public void AClosedIntake_ShouldNameTheDeadlineItPassed()
    {
        // Arrange
        var closing = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(2));

        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Closed, closing));

        // Assert
        // D12: the value that decided it, in the time the reader lives in, not
        // a restatement of the rule. Whoever reads this at 12:05 can see that
        // they were five minutes late rather than guess at it.
        Assert.Equal(
            "Nabór został zamknięty 30.09.2026 o godzinie 12:00 "
            + "czasu polskiego. Wniosku nie można już złożyć.",
            message);
    }

    [Fact]
    public void AnOpenIntake_ShouldNameTheDeadlineItIsRunningTowards()
    {
        // Arrange
        var closing = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(2));

        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Open, closing));

        // Assert
        Assert.Equal(
            "Nabór trwa. Wnioski można składać do 30.09.2026 "
            + "o godzinie 12:00 czasu polskiego.",
            message);
    }

    [Fact]
    public void AnIntakeThatHasNotOpened_ShouldNameTheMomentItWill()
    {
        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.NotYetOpen, Start.AddDays(30)));

        // Assert
        // 08:00 UTC on the first of September is 10:00 in Warsaw, and the
        // sentence says the hour the applicant will be watching.
        Assert.Equal(
            "Nabór jeszcze się nie rozpoczął. Wnioski można składać od "
            + "01.09.2026 o godzinie 10:00 czasu polskiego.",
            message);
    }

    [Theory]

    // The same wall clock hour on either side of the switch, entered by an
    // operator who never thinks about offsets, is two different instants. Both
    // come back named by the hour that was typed.
    [InlineData("2026-10-20T10:00:00Z", "20.10.2026 o godzinie 12:00")]
    [InlineData("2026-11-03T11:00:00Z", "03.11.2026 o godzinie 12:00")]

    // Half past two in the morning on the night of the switch happens twice.
    // The first pass is the one this instant names, and it says so rather than
    // shifting the date.
    [InlineData("2026-10-25T00:30:00Z", "25.10.2026 o godzinie 02:30")]
    public void TheDeadline_ShouldKeepItsLocalHourAcrossTheOctoberSwitch(
        string closing,
        string expected)
    {
        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Closed, DateTimeOffset.Parse(closing)));

        // Assert
        Assert.Contains(expected + " czasu polskiego", message);
    }

    [Fact]
    public void AContinuousIntake_ShouldSayThereIsNoDeadlineAtAll()
    {
        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Open, closesAt: null));

        // Assert
        // No invented date, and no empty spot where one would go.
        Assert.Equal(
            "Nabór ciągły. Wnioski można składać bez terminu końcowego.",
            message);
    }

    [Fact]
    public void AContinuousIntakeClosedByHand_ShouldRefuseWithoutNamingADate()
    {
        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Closed, closesAt: null));

        // Assert
        Assert.Equal(
            "Nabór został zamknięty. Wniosku nie można już złożyć.",
            message);
    }

    [Fact]
    public void ACompetitionWithNoIntake_ShouldSaySoWithoutInventingAReason()
    {
        // Act
        var message = CompetitionIntakeMessage.For(
            Intake(IntakeState.Unavailable, closesAt: null));

        // Assert
        // A draft or a deactivated competition. Neither has a deadline that
        // passed, so neither gets a sentence with a date in it.
        Assert.Equal("Ten konkurs nie przyjmuje wniosków.", message);
    }

    [Fact]
    public void EveryState_ShouldHaveSomethingToSay()
    {
        // Act
        var silent = Enum.GetValues<IntakeState>()
            .Where(state => string.IsNullOrWhiteSpace(
                CompetitionIntakeMessage.For(Intake(state, Start.AddDays(30)))))
            .ToList();

        // Assert
        // The card asks for an unambiguous error, not an empty answer, so a
        // state added later must not fall through to one.
        Assert.Empty(silent);
    }
}
