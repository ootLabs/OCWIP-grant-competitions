using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Models;

/// <summary>
/// The table of allowed pairs (T-20, R-17). These tests are about the table as
/// a SET, not about individual rows: the point of gathering the lifecycle in
/// one place is that questions like "can anything still happen to an archived
/// competition" have an answer you can read off, and an answer that stays true
/// when somebody adds a state.
/// </summary>
public sealed class CompetitionStatusTransitionsTests
{
    [Fact]
    public void TheTable_ShouldNotRepeatAPair()
    {
        // Act
        var duplicates = CompetitionStatusTransitions.All
            .GroupBy(transition =>
                (transition.From, transition.To, transition.Trigger))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert
        // A repeated row changes nothing about behaviour, which is exactly why
        // it is worth catching: it reads as two rules and is one.
        Assert.Empty(duplicates);
    }

    [Fact]
    public void AnOperator_ShouldNotBeAbleToOpenAnIntakeEarly()
    {
        // Assert
        // Published to OpenForApplications is in the table, but only as a
        // Schedule row. If AllowsOperator answered "is this pair listed" then
        // an operator could open an intake before its start date, which is the
        // one thing the dates are there to decide.
        Assert.Contains(
            CompetitionStatusTransitions.All,
            transition =>
                transition.From == CompetitionStatus.Published
                && transition.To == CompetitionStatus.OpenForApplications);

        Assert.False(CompetitionStatusTransitions.AllowsOperator(
            CompetitionStatus.Published,
            CompetitionStatus.OpenForApplications));
    }

    [Theory]
    [InlineData(CompetitionStatus.Draft, CompetitionStatus.Published)]
    [InlineData(CompetitionStatus.OpenForApplications, CompetitionStatus.Closed)]
    [InlineData(CompetitionStatus.Closed, CompetitionStatus.UnderReview)]
    [InlineData(CompetitionStatus.UnderReview, CompetitionStatus.Resolved)]
    [InlineData(CompetitionStatus.Resolved, CompetitionStatus.Archived)]
    public void TheOperatorPath_ShouldRunTheWholeLengthOfTheLifecycle(
        CompetitionStatus from,
        CompetitionStatus to)
    {
        // Assert
        Assert.True(CompetitionStatusTransitions.AllowsOperator(from, to));
    }

    [Theory]
    [InlineData(CompetitionStatus.Published, CompetitionStatus.Draft)]
    [InlineData(CompetitionStatus.Archived, CompetitionStatus.Published)]
    [InlineData(CompetitionStatus.Resolved, CompetitionStatus.UnderReview)]
    [InlineData(CompetitionStatus.Draft, CompetitionStatus.Resolved)]
    [InlineData(CompetitionStatus.Published, CompetitionStatus.Published)]
    public void GoingBackwards_OrSkipping_ShouldNotBeAllowed(
        CompetitionStatus from,
        CompetitionStatus to)
    {
        // Assert
        // Unpublishing is the one worth naming: a published competition is
        // already visible and may already have applications against it, so the
        // way out is Closed and then the rest of the sequence, not a quiet
        // return to Draft.
        Assert.False(CompetitionStatusTransitions.AllowsOperator(from, to));
    }

    [Fact]
    public void Archived_ShouldBeTheEndOfTheLine()
    {
        // Assert
        Assert.Empty(
            CompetitionStatusTransitions.OperatorTargets(CompetitionStatus.Archived));
        Assert.Null(
            CompetitionStatusTransitions.ScheduledTarget(CompetitionStatus.Archived));
    }

    [Fact]
    public void EveryStateExceptTheFirst_ShouldBeReachable()
    {
        // Arrange
        var reachable = CompetitionStatusTransitions.All
            .Select(transition => transition.To)
            .Distinct()
            .ToHashSet();

        // Act
        var unreachable = Enum.GetValues<CompetitionStatus>()
            .Where(status =>
                status != CompetitionStatus.Draft
                && !reachable.Contains(status))
            .ToList();

        // Assert
        // A state nothing leads to is a state the product cannot be in, and
        // the enum will not say so on its own. This is the guard for the next
        // person adding a value.
        Assert.Empty(unreachable);
    }

    [Fact]
    public void OnlyTheIntakeWindow_ShouldBeDrivenByTheClock()
    {
        // Act
        var scheduled = CompetitionStatusTransitions.All
            .Where(transition => transition.Trigger == TransitionTrigger.Schedule)
            .Select(transition => (transition.From, transition.To))
            .ToList();

        // Assert
        // The report is specific: everything happens by itself EXCEPT
        // resolving and archiving. In this model the intake window is the only
        // thing the calendar knows enough to decide, and the rest waits for a
        // person. Adding a Schedule row means adding something that fires with
        // nobody watching, so it should have to break this test first.
        Assert.Equal(
            [
                (CompetitionStatus.Published, CompetitionStatus.OpenForApplications),
                (CompetitionStatus.OpenForApplications, CompetitionStatus.Closed),
            ],
            scheduled);
    }

    [Fact]
    public void ClosingAnIntake_ShouldBeOpenToBothAnOperatorAndTheClock()
    {
        // Assert
        // Not a duplicated row: a continuous intake has no closing date, so
        // the clock never reaches it and without the operator row it could
        // never be closed at all.
        Assert.True(CompetitionStatusTransitions.AllowsOperator(
            CompetitionStatus.OpenForApplications,
            CompetitionStatus.Closed));

        Assert.Equal(
            CompetitionStatus.Closed,
            CompetitionStatusTransitions.ScheduledTarget(
                CompetitionStatus.OpenForApplications));
    }
}
