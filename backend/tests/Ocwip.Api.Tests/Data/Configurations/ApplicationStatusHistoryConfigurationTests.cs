using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// What the schema enforces for the append-only status history (T-33): the
/// transition check constraint, the foreign keys that never cascade, and the
/// working index an operator or an applicant reads a timeline from.
/// </summary>
public sealed class ApplicationStatusHistoryConfigurationTests
{
    private static IEntityType GetEntityType() =>
        TestModel.EntityType<ApplicationStatusHistory>();

    [Fact]
    public void ShouldMapToTheSnakeCaseTable()
    {
        // Act
        var tableName = GetEntityType().GetTableName();

        // Assert
        Assert.Equal("application_status_history", tableName);
    }

    [Fact]
    public void ShouldHaveIdAsPrimaryKeyWithADatabaseGeneratedUuid()
    {
        // Act
        var primaryKey = GetEntityType().FindPrimaryKey();
        var id = GetEntityType().FindProperty(nameof(ApplicationStatusHistory.Id));

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(ApplicationStatusHistory.Id), primaryKey.Properties[0].Name);
        Assert.Equal("gen_random_uuid()", id!.GetDefaultValueSql());
    }

    [Fact]
    public void FromAndToStatus_ShouldBeStoredAsRequiredText()
    {
        // Act
        var from = GetEntityType().FindProperty(nameof(ApplicationStatusHistory.FromStatus))!;
        var to = GetEntityType().FindProperty(nameof(ApplicationStatusHistory.ToStatus))!;

        // Assert
        // Text, not the enum ordinal, same reasoning as Application.Status: the
        // check constraint below compares against the text.
        Assert.False(from.IsNullable);
        Assert.Equal(typeof(string), from.GetProviderClrType());
        Assert.False(to.IsNullable);
        Assert.Equal(typeof(string), to.GetProviderClrType());
    }

    [Fact]
    public void ShouldRefuseATransitionThatDoesNotChangeAnything()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_application_status_history_from_ne_to");

        // Assert
        Assert.NotNull(constraint);
        Assert.Equal("from_status <> to_status", constraint.Sql);
    }

    [Fact]
    public void ShouldHaveIndexOnApplicationIdAndChangedAt()
    {
        // Act
        var index = GetEntityType()
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Select(p => p.Name)
                    .SequenceEqual(
                    [
                        nameof(ApplicationStatusHistory.ApplicationId),
                        nameof(ApplicationStatusHistory.ChangedAt)
                    ]));

        // Assert
        // The read this table exists for: one application's whole timeline, in
        // order.
        Assert.NotNull(index);
    }

    [Theory]
    [InlineData(nameof(ApplicationStatusHistory.Application))]
    [InlineData(nameof(ApplicationStatusHistory.ChangedByUser))]
    public void EveryRelationship_ShouldRefuseToCascade(string navigationName)
    {
        // Act
        var navigation = GetEntityType().FindNavigation(navigationName);

        // Assert
        // docs/model-danych.md rule 1: zero ON DELETE CASCADE. Deactivating an
        // application, or an account, must not take this history with it.
        Assert.NotNull(navigation);
        Assert.Equal(DeleteBehavior.NoAction, navigation.ForeignKey.DeleteBehavior);
    }
}
