using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Configurations;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class FormDefinitionConfigurationTests
{
    private static IMutableEntityType GetEntityType()
    {
        var modelBuilder = new ModelBuilder();

        new FormDefinitionConfiguration().Configure(
            modelBuilder.Entity<FormDefinition>());

        return modelBuilder.Model.FindEntityType(
            typeof(FormDefinition))!;
    }

    [Fact]
    public void FormDefinition_ShouldHaveIdAsPrimaryKey()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var primaryKey = entityType.FindPrimaryKey();

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(
            nameof(FormDefinition.Id),
            primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Id_ShouldHaveDatabaseGeneratedUuidDefault()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(FormDefinition.Id));

        // Assert
        Assert.NotNull(property);
        Assert.Equal(
            "gen_random_uuid()",
            property.GetDefaultValueSql());
    }

    [Fact]
    public void VersionNumber_ShouldBeRequired()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(FormDefinition.VersionNumber));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void Definition_ShouldBeRequired()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(FormDefinition.Definition));

        // Assert
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void Definition_ShouldUseJsonbColumnType()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var property = entityType.FindProperty(
            nameof(FormDefinition.Definition));

        // Assert
        Assert.NotNull(property);
        Assert.Equal(
            "jsonb",
            property.GetColumnType());
    }


    [Fact]
    public void ShouldHaveUniqueIndexOnCompetitionIdAndVersionNumber()
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
                        nameof(FormDefinition.CompetitionId),
                        nameof(FormDefinition.VersionNumber)
                    ]));

        // Assert
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }
}
