using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

public sealed class AttachmentConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Attachment>();

    private static IProperty GetProperty(string name) =>
        GetEntityType().FindProperty(name)
        ?? throw new InvalidOperationException($"{name} is not mapped.");

    [Fact]
    public void Attachment_ShouldMapToThePluralSnakeCaseTable()
    {
        // Act
        var tableName = GetEntityType().GetTableName();

        // Assert
        Assert.Equal("attachments", tableName);
    }

    [Fact]
    public void Attachment_ShouldHaveIdAsPrimaryKeyWithAUuidDefault()
    {
        // Act
        var primaryKey = GetEntityType().FindPrimaryKey();
        var id = GetProperty(nameof(Attachment.Id));

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Equal(nameof(Attachment.Id), primaryKey.Properties.Single().Name);
        Assert.Equal("gen_random_uuid()", id.GetDefaultValueSql());
    }

    [Theory]
    [InlineData(nameof(Attachment.FileName), 255)]
    [InlineData(nameof(Attachment.ContentType), 255)]
    [InlineData(nameof(Attachment.StoragePath), 500)]
    public void EveryTextColumn_ShouldBeRequiredAndBounded(
        string propertyName,
        int maxLength)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        // The card asks for file name, MIME type, size and storage path, and
        // none of them may be unbounded text.
        Assert.False(property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    [Fact]
    public void SizeInBytes_ShouldBeARequiredBigint()
    {
        // Act
        var property = GetProperty(nameof(Attachment.SizeInBytes));

        // Assert
        // long, not int: an int caps a file at 2 GB, and the limit belongs to
        // T-32 rather than to the width of the column.
        Assert.False(property.IsNullable);
        Assert.Equal(typeof(long), property.ClrType);
    }

    [Fact]
    public void ContentType_ShouldSayThatItIsNotVerified()
    {
        // Act
        var comment = GetProperty(nameof(Attachment.ContentType)).GetComment();

        // Assert
        // A client controlled content type proves nothing about the bytes. The
        // comment has to name the card that owns checking it, otherwise the next
        // reader treats the column as trustworthy.
        Assert.NotNull(comment);
        Assert.Contains("T-32", comment);
    }

    [Fact]
    public void StoragePath_ShouldSayThatItMustNotBeGuessable()
    {
        // Act
        var comment = GetProperty(nameof(Attachment.StoragePath)).GetComment();

        // Assert
        // An attachment is another organisation's document. A file reachable
        // because somebody knows the link is a leak, so the constraint on the
        // shape of this value is recorded where the column is defined.
        Assert.NotNull(comment);
        Assert.Contains("guessable", comment);
        Assert.Contains("T-32", comment);
    }

    [Fact]
    public void ShouldHavePositiveSizeCheckConstraint()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x => x.Name == "ck_attachments_size_in_bytes_positive");

        // Assert
        // A zero byte attachment is a failed upload, not a document.
        Assert.NotNull(constraint);
        Assert.Equal("size_in_bytes > 0", constraint.Sql);
    }

    [Fact]
    public void ShouldHaveSoftDeletePairingCheckConstraint()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_attachments_deactivated_at_matches_is_active");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("is_active = (deactivated_at IS NULL)", constraint.Sql);
    }

    [Fact]
    public void EveryCheckConstraint_ShouldSurviveASingleToTableCall()
    {
        // Act
        var constraints = GetEntityType().GetCheckConstraints().ToList();

        // Assert
        Assert.Equal(2, constraints.Count);
    }

    [Fact]
    public void ShouldHaveUniqueIndexOnStoragePath()
    {
        // Act
        var index = GetEntityType()
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Single().Name == nameof(Attachment.StoragePath));

        // Assert
        // Two rows pointing at one blob turn deleting a file into a way of
        // breaking a different application's attachment.
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal("ix_attachments_storage_path", index.GetDatabaseName());
    }

    [Fact]
    public void Application_ShouldBeReferencedWithoutCascade()
    {
        // Act
        var navigation = GetEntityType().FindNavigation(nameof(Attachment.Application));

        // Assert
        Assert.NotNull(navigation);
        Assert.Equal(
            nameof(Attachment.ApplicationId),
            navigation.ForeignKey.Properties.Single().Name);

        // docs/model-danych.md rule 1: zero ON DELETE CASCADE.
        Assert.Equal(DeleteBehavior.NoAction, navigation.ForeignKey.DeleteBehavior);
    }

    [Theory]
    [InlineData(nameof(Attachment.CreatedAt))]
    [InlineData(nameof(Attachment.UpdatedAt))]
    [InlineData(nameof(Attachment.DeactivatedAt))]
    public void EveryTimestamp_ShouldUseTimestampWithTimeZoneAndTheUtcConverter(
        string propertyName)
    {
        // Act
        var property = GetProperty(propertyName);

        // Assert
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }
}
