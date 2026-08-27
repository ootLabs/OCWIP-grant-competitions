using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class CompetitionConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Competition>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

    [Fact]
    public void Competition_ShouldMapToThePluralSnakeCaseTable()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var tableName = entityType.GetTableName();

        // Assert
        // docs/konwencje.md: tables are plural. docs/model-danych.md names this
        // table competitions. An explicit ToTable("competition") used to break it.
        Assert.Equal("competitions", tableName);
    }

    [Fact]
    public void Competition_ShouldHaveIdAsPrimaryKey()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var primaryKey = entityType.FindPrimaryKey();

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(Competition.Id), primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Id_ShouldHaveDatabaseGeneratedUuidDefault()
    {
        // Act
        var property = GetProperty(nameof(Competition.Id));

        // Assert
        Assert.Equal("gen_random_uuid()", property.GetDefaultValueSql());
    }

    [Fact]
    public void Title_ShouldBeRequiredAndBoundedWideEnoughForARealTitle()
    {
        // Act
        var property = GetProperty(nameof(Competition.Title));

        // Assert
        // Bounded, but not so tight that a real title has to be abbreviated:
        // "Konkurs ofert na realizację zadań publicznych w zakresie wspierania
        // inicjatyw obywatelskich w 2026 roku" is already over 100 characters.
        Assert.False(property.IsNullable);
        Assert.Equal(200, property.GetMaxLength());
    }

    [Fact]
    public void Description_ShouldBeOptionalAndFitAnAnnouncementBody()
    {
        // Act
        var property = GetProperty(nameof(Competition.Description));

        // Assert
        // This is the announcement body, not a summary line.
        Assert.True(property.IsNullable);
        Assert.Equal(10000, property.GetMaxLength());
    }

    [Fact]
    public void Status_ShouldBeStoredAsBoundedString()
    {
        // Act
        var property = GetProperty(nameof(Competition.Status));

        // Assert
        // HasConversion<string>() records the provider type; the concrete
        // converter is picked from the type mapping later, so both shapes count.
        var providerType =
            property.GetProviderClrType()
            ?? property.GetValueConverter()?.ProviderClrType;

        Assert.Equal(typeof(string), providerType);
        Assert.Equal(20, property.GetMaxLength());
        Assert.False(property.IsNullable);
    }

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

    [Fact]
    public void MaxGrantAmount_ShouldBeRequiredAndHavePrecision18Scale2()
    {
        // Act
        var property = GetProperty(nameof(Competition.MaxGrantAmount));

        // Assert
        Assert.False(property.IsNullable);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public void MaxGrantAmount_ShouldHaveExpectedComment()
    {
        // Act
        var property = GetProperty(nameof(Competition.MaxGrantAmount));

        // Assert
        Assert.Equal(
            "Maximum grant amount allowed for the competition. " +
            "Used later to validate the application budget.",
            property.GetComment());
    }

    [Fact]
    public void IsActive_ShouldBeRequiredAndDeactivatedAtOptional()
    {
        // Act
        var isActive = GetProperty(nameof(Competition.IsActive));
        var deactivatedAt = GetProperty(nameof(Competition.DeactivatedAt));

        // Assert
        // AGENTS.md security rule 5: no hard deletes, retention of 5 years, so
        // deletion is a flag. DeactivatedAt stays null while the row is active
        // instead of carrying a default 0001-01-01.
        Assert.False(isActive.IsNullable);
        Assert.True(deactivatedAt.IsNullable);
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

    [Fact]
    public void ShouldHaveStartDateBeforeEndDateCheckConstraint()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_competitions_start_date_before_end_date");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("start_date < end_date", constraint.Sql);
    }

    [Fact]
    public void ShouldHavePositiveMaxGrantAmountCheckConstraint()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_competitions_max_grant_amount_positive");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("max_grant_amount > 0", constraint.Sql);
    }

    [Theory]
    [InlineData("ck_competitions_start_date_whole_minute", "start_date")]
    [InlineData("ck_competitions_end_date_whole_minute", "end_date")]
    public void ShouldHaveWholeMinuteCheckConstraint(string name, string column)
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x => x.Name == name);

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal(
            $"date_trunc('minute', {column} AT TIME ZONE 'UTC') "
            + $"= {column} AT TIME ZONE 'UTC'",
            constraint.Sql);
    }

    [Fact]
    public void ShouldHaveSoftDeletePairingCheckConstraint()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_competitions_deactivated_at_matches_is_active");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("is_active = (deactivated_at IS NULL)", constraint.Sql);
    }

    [Fact]
    public void EveryCheckConstraint_ShouldSurviveASingleToTableCall()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraints = entityType.GetCheckConstraints().ToList();

        // Assert
        // Two separate builder.ToTable calls reconfigure the table instead of
        // adding to it, which silently drops the constraints of the first call.
        Assert.Equal(5, constraints.Count);
    }

    [Fact]
    public void ShouldHaveIndexOnStatusAndEndDate()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var index = entityType
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Select(p => p.Name)
                    .SequenceEqual(
                    [
                        nameof(Competition.Status),
                        nameof(Competition.EndDate)
                    ]));

        // Assert
        // The public listing filters on status plus closing date, so without
        // this index it turns into a sequential scan once the board fills up.
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
        Assert.Equal("ix_competitions_status_end_date", index.GetDatabaseName());
    }

    [Fact]
    public void FormDefinitions_ShouldHaveCompetitionRelationshipWithoutCascade()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var navigation = entityType.FindNavigation(
            nameof(Competition.FormDefinitions));

        // Assert
        Assert.NotNull(navigation);

        var foreignKey = navigation.ForeignKey;

        Assert.Equal(
            nameof(Competition.Id),
            foreignKey.PrincipalKey.Properties.Single().Name);

        Assert.Equal(
            nameof(FormDefinition.CompetitionId),
            foreignKey.Properties.Single().Name);

        // docs/model-danych.md rule 1: zero ON DELETE CASCADE.
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }
}
