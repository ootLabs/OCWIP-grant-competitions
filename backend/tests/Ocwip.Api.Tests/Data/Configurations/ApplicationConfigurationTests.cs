using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class ApplicationConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Application>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

    [Fact]
    public void Application_ShouldMapToThePluralSnakeCaseTable()
    {
        // Act
        var tableName = GetEntityType().GetTableName();

        // Assert
        // docs/model-danych.md names this table applications.
        Assert.Equal("applications", tableName);
    }

    [Fact]
    public void Application_ShouldHaveIdAsPrimaryKey()
    {
        // Act
        var primaryKey = GetEntityType().FindPrimaryKey();

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(Application.Id), primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Id_ShouldHaveDatabaseGeneratedUuidDefault()
    {
        // Act
        var property = GetProperty(nameof(Application.Id));

        // Assert
        // docs/model-danych.md rule 3: a UUID, not a sequence. The identifier
        // ends up in a URL, and a sequence lets a competitor count the
        // applications that arrived and guess somebody else's.
        Assert.Equal("gen_random_uuid()", property.GetDefaultValueSql());
    }

    [Fact]
    public void Answers_ShouldBeRequiredJsonb()
    {
        // Act
        var property = GetProperty(nameof(Application.Answers));

        // Assert
        // Not columns: the shape follows the form definition, and the form has 5
        // to 6 pages that every competition may shape differently.
        Assert.False(property.IsNullable);
        Assert.Equal("jsonb", property.GetColumnType());
    }

    [Fact]
    public void Answers_ShouldPointAtTheCardThatDefinesItsContract()
    {
        // Act
        var comment = GetProperty(nameof(Application.Answers)).GetComment();

        // Assert
        // The column ships without a schema on purpose, so the comment has to
        // name both the card that settles it and the encryption that is owed.
        Assert.NotNull(comment);
        Assert.Contains("T-20", comment);
        Assert.Contains("T-80", comment);
    }

    [Fact]
    public void Answers_ShouldNotBeADisposableJsonDocument()
    {
        // Act
        var clrType = GetProperty(nameof(Application.Answers)).ClrType;

        // Assert
        // EF never disposes materialized instances, and JsonDocument is pooled
        // and disposable, so a listing query would leak one per row.
        Assert.False(typeof(IDisposable).IsAssignableFrom(clrType));
    }

    [Fact]
    public void Status_ShouldBeStoredAsText()
    {
        // Act
        var property = GetProperty(nameof(Application.Status));

        // Assert
        // Text, not the enum ordinal: reordering ApplicationStatus would
        // reinterpret every existing row, and the check constraints pairing the
        // status with the submission date compare against the text.
        Assert.False(property.IsNullable);
        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.Equal(20, property.GetMaxLength());
    }

    [Fact]
    public void Number_ShouldBeOptionalAndBounded()
    {
        // Act
        var property = GetProperty(nameof(Application.Number));

        // Assert
        // Optional because a draft has no number: it is assigned at submission.
        Assert.True(property.IsNullable);
        Assert.Equal(50, property.GetMaxLength());
    }

    [Fact]
    public void SubmittedAt_ShouldBeOptional()
    {
        // Act
        var property = GetProperty(nameof(Application.SubmittedAt));

        // Assert
        // Nullable, so a draft never carries 0001-01-01, which would look like
        // data and pass every validation.
        Assert.True(property.IsNullable);
    }

    [Theory]
    [InlineData(nameof(Application.SubmittedAt))]
    [InlineData(nameof(Application.CreatedAt))]
    [InlineData(nameof(Application.UpdatedAt))]
    [InlineData(nameof(Application.DeactivatedAt))]
    public void EveryTimestamp_ShouldUseTimestampWithTimeZoneAndTheUtcConverter(
        string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Theory]
    [InlineData(nameof(Application.CreatedAt))]
    [InlineData(nameof(Application.UpdatedAt))]
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
    public void IsActive_ShouldBeRequiredAndDeactivatedAtOptional()
    {
        // Act
        var isActive = GetProperty(nameof(Application.IsActive));
        var deactivatedAt = GetProperty(nameof(Application.DeactivatedAt));

        // Assert
        Assert.False(isActive.IsNullable);
        Assert.True(deactivatedAt.IsNullable);
    }
}
