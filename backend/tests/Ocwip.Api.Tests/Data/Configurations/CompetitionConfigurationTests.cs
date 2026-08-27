using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Configurations;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class CompetitionConfigurationTests
{
    private static IMutableEntityType GetEntityType()
    {
        var modelBuilder = new ModelBuilder();

        new CompetitionConfiguration().Configure(
            modelBuilder.Entity<Competition>());

        return modelBuilder.Model.FindEntityType(typeof(Competition))!;
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
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.Id));

        // Assert
        Assert.NotNull(property);
        Assert.Equal(
            "gen_random_uuid()",
            property.GetDefaultValueSql());
    }

    [Fact]
    public void Title_ShouldBeRequiredAndHaveMaximumLength50()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.Title));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(50, property.GetMaxLength());
    }

    [Fact]
    public void Description_ShouldHaveMaximumLength500()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.Description));

        // Assert
        Assert.NotNull(property);
        Assert.Equal(500, property.GetMaxLength());
    }

    [Fact]
    public void Status_ShouldBeStoredAsString()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.Status));

        // Assert
        Assert.NotNull(property);
        Assert.NotNull(property.GetValueConverter());

        Assert.Equal(
            typeof(string),
            property.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void StartDate_ShouldBeRequiredAndUseTimestampWithTimeZone()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.StartDate));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(
            "timestamp with time zone",
            property.GetColumnType());
    }

    [Fact]
    public void EndDate_ShouldBeRequiredAndUseTimestampWithTimeZone()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(nameof(Competition.EndDate));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(
            "timestamp with time zone",
            property.GetColumnType());
    }

    

    [Fact]
    public void MaxGrantAmount_ShouldBeRequiredAndHavePrecision18Scale2()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(Competition.MaxGrantAmount));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public void MaxGrantAmount_ShouldHaveExpectedComment()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(Competition.MaxGrantAmount));

        // Assert
        Assert.Equal(
            "Maximum grant amount allowed for the competition. " +
            "Used later to validate the application budget.",
            property!.GetComment());
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
                x.Name == "ck_competition_start_date_before_end_date");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal(
            "start_date < end_date",
            constraint.Sql);
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
                x.Name == "ck_maxgrantamount_greater_than_0");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal(
            "max_grant_amount > 0",
            constraint.Sql);
    }

    [Fact]
    public void FormDefinitions_ShouldHaveCompetitionRelationship()
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

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }
}
