using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// How the competition treats time: audit stamps keep full precision, the
/// window is truncated to a whole minute, and truncation must not sit in a
/// value converter because EF would apply it to query operands too.
/// </summary>
public sealed class CompetitionTimestampConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Competition>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

    [Theory]
    [InlineData(nameof(Competition.CreatedAt))]
    [InlineData(nameof(Competition.UpdatedAt))]
    [InlineData(nameof(Competition.DeactivatedAt))]
    public void AuditTimestamps_ShouldKeepFullPrecisionInUtc(string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        // "When exactly did this happen", so no truncation here.
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Theory]
    [InlineData(nameof(Competition.CreatedAt))]
    [InlineData(nameof(Competition.UpdatedAt))]
    public void AuditTimestamps_ShouldBeFilledByTheDatabaseWhenOmitted(
        string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        Assert.False(property.IsNullable);
        Assert.Equal("now()", property.GetDefaultValueSql());
    }

    [Theory]
    [InlineData(nameof(Competition.StartDate))]
    [InlineData(nameof(Competition.EndDate))]
    public void TheCompetitionWindow_ShouldNotTruncateThroughAValueConverter(
        string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        // EF applies a property converter to the operand of a comparison as
        // well, so a truncating converter here would rewrite "EndDate >= now"
        // at 12:00:45 into "EndDate >= 12:00:00" and a competition closing at
        // 12:00 would keep matching for another 59 seconds. Truncation lives in
        // the entity setter; only the instant preserving UTC converter is left.
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Theory]
    [InlineData(nameof(Competition.StartDate))]
    [InlineData(nameof(Competition.EndDate))]
    public void StartAndEndDate_ShouldBeRequired(string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void TheWindowSetter_ShouldDropSecondsAndMicroseconds()
    {
        // Arrange
        // 12:00:59.999999 local, which an operator meant as the 12:00 deadline.
        var typed = new DateTimeOffset(
            2026, 9, 1, 12, 0, 59, 999, TimeSpan.FromHours(2)).AddTicks(9990);

        // Act
        var competition = new Competition { StartDate = typed, EndDate = typed };

        // Assert
        var expected = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, competition.StartDate);
        Assert.Equal(expected, competition.EndDate);
        Assert.Equal(TimeSpan.Zero, competition.StartDate.Offset);
    }

    [Fact]
    public void UtcConverter_ShouldShiftAnOffsetToUtcBeforeItReachesNpgsql()
    {
        // Arrange
        // Npgsql accepts only Offset == 0 for timestamptz, so the conversion has
        // to happen in the model, not in a comment. Asserted on an audit
        // timestamp, because the competition window uses the truncating variant.
        var converter = Assert.IsType<UtcDateTimeOffsetConverter>(
            GetProperty(nameof(Competition.CreatedAt)).GetValueConverter());

        var polishSummerTime =
            new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2));

        // Act
        var stored = (DateTimeOffset)converter.ConvertToProvider(polishSummerTime)!;

        // Assert
        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(polishSummerTime.UtcDateTime, stored.UtcDateTime);
        Assert.Equal(8, stored.Hour);
    }
}
