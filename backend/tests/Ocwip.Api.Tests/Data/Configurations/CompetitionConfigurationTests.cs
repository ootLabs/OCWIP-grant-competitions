using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// The competition table and its columns as the real EF model describes them:
/// name, key, widths and how the status is stored.
/// </summary>
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
}
