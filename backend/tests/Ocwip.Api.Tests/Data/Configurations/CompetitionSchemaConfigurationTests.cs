using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// What the schema itself enforces for a competition: every check constraint,
/// the index the public listing needs, and a foreign key that never cascades.
/// </summary>
public sealed class CompetitionSchemaConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Competition>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

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
