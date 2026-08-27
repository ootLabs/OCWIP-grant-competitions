using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class FormDefinitionConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<FormDefinition>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

    [Fact]
    public void FormDefinition_ShouldMapToThePluralSnakeCaseTable()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var tableName = entityType.GetTableName();

        // Assert
        // docs/model-danych.md names this table form_definitions.
        Assert.Equal("form_definitions", tableName);
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
        Assert.Equal(nameof(FormDefinition.Id), primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Id_ShouldHaveDatabaseGeneratedUuidDefault()
    {
        // Act
        var property = GetProperty(nameof(FormDefinition.Id));

        // Assert
        Assert.Equal("gen_random_uuid()", property.GetDefaultValueSql());
    }

    [Fact]
    public void VersionNumber_ShouldBeRequired()
    {
        // Act
        var property = GetProperty(nameof(FormDefinition.VersionNumber));

        // Assert
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void Definition_ShouldBeRequiredJsonb()
    {
        // Act
        var property = GetProperty(nameof(FormDefinition.Definition));

        // Assert
        Assert.False(property.IsNullable);
        Assert.Equal("jsonb", property.GetColumnType());
    }

    [Fact]
    public void Definition_ShouldPointAtTheCardThatDefinesItsContract()
    {
        // Act
        var comment = GetProperty(nameof(FormDefinition.Definition)).GetComment();

        // Assert
        // The column ships without a schema on purpose. The comment has to name
        // the card that settles it, otherwise the next person reads an empty
        // jsonb column and starts guessing.
        Assert.NotNull(comment);
        Assert.Contains("T-20", comment);
    }

    [Fact]
    public void Definition_ShouldNotBeADisposableJsonDocument()
    {
        // Act
        var clrType = GetProperty(nameof(FormDefinition.Definition)).ClrType;

        // Assert
        // EF never disposes materialized instances, and JsonDocument is pooled
        // and disposable, so a listing query would leak one per row.
        Assert.False(typeof(IDisposable).IsAssignableFrom(clrType));
    }

    [Theory]
    [InlineData(nameof(FormDefinition.CreatedAt))]
    [InlineData(nameof(FormDefinition.UpdatedAt))]
    [InlineData(nameof(FormDefinition.DeactivatedAt))]
    public void EveryTimestamp_ShouldUseTimestampWithTimeZoneAndTheUtcConverter(
        string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Fact]
    public void IsActive_ShouldBeRequiredAndDeactivatedAtOptional()
    {
        // Act
        var isActive = GetProperty(nameof(FormDefinition.IsActive));
        var deactivatedAt = GetProperty(nameof(FormDefinition.DeactivatedAt));

        // Assert
        Assert.False(isActive.IsNullable);
        Assert.True(deactivatedAt.IsNullable);
    }

    [Theory]
    [InlineData(nameof(FormDefinition.CreatedAt))]
    [InlineData(nameof(FormDefinition.UpdatedAt))]
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
    public void ShouldHaveSoftDeletePairingCheckConstraint()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_form_definitions_deactivated_at_matches_is_active");

        // Assert
        // The same pairing as on Competition: soft delete is two columns and
        // neither may move without the other.
        Assert.NotNull(constraint);
        Assert.Equal("is_active = (deactivated_at IS NULL)", constraint.Sql);
    }

    [Fact]
    public void ShouldHavePositiveVersionNumberCheckConstraint()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_form_definitions_version_number_positive");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("version_number > 0", constraint.Sql);
    }

    [Fact]
    public void ShouldRequireTheDefinitionToBeAJsonDocument()
    {
        // Arrange
        var entityType = GetEntityType();

        // Act
        var constraint = entityType
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_form_definitions_definition_is_a_document");

        // Assert
        // Object or array, not one of them: which root the contract picks is
        // decided in T-20, and rejecting scalars prejudges neither.
        Assert.NotNull(constraint);
        Assert.Equal(
            "jsonb_typeof(definition) IN ('object', 'array')",
            constraint.Sql);
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

        // The database name is asserted because the integration test matches on
        // it when it checks which constraint refused the insert.
        Assert.Equal(
            "ix_form_definitions_competition_id_version_number",
            index.GetDatabaseName());
    }
}
