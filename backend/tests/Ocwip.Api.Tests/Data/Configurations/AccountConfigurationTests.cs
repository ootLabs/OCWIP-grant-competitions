using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Data.Converters;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// Metadata of the two entities an application hangs off: the account and the
/// entity that files it. They were written as plain classes in T-11.2 and only
/// reached the schema together with the application, so several of these tests
/// are regressions against the shape they had before.
/// </summary>
public sealed class AccountConfigurationTests
{
    private static IProperty UserProperty(string name) =>
        TestModel.EntityType<User>().FindProperty(name)
        ?? throw new InvalidOperationException($"User.{name} is not mapped.");

    private static IProperty EntityProperty(string name) =>
        TestModel.EntityType<Entity>().FindProperty(name)
        ?? throw new InvalidOperationException($"Entity.{name} is not mapped.");

    [Theory]
    [InlineData(typeof(User), "users")]
    [InlineData(typeof(Entity), "entities")]
    public void EveryAccountTable_ShouldBePluralAndSnakeCase(
        Type clrType,
        string expected)
    {
        // Act
        var tableName = TestModel.Model.FindEntityType(clrType)?.GetTableName();

        // Assert
        // docs/model-danych.md names these tables users and entities.
        Assert.Equal(expected, tableName);
    }

    [Fact]
    public void Email_ShouldBeUniqueInTheDatabase()
    {
        // Act
        var index = TestModel.EntityType<User>()
            .GetIndexes()
            .SingleOrDefault(x => x.Properties.Single().Name == nameof(User.Email));

        // Assert
        // In the database and not in application code: a SELECT before an INSERT
        // loses the race against a second registration, and two accounts on one
        // address break password reset.
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal("ix_users_email", index.GetDatabaseName());
    }

    [Fact]
    public void Role_ShouldBeStoredAsText()
    {
        // Act
        var property = UserProperty(nameof(User.Role));

        // Assert
        // Text, not the enum ordinal, for the same reason as CompetitionStatus:
        // reordering Role would silently reinterpret every existing row.
        Assert.Equal(typeof(string), property.GetProviderClrType());
    }

    [Fact]
    public void Type_ShouldBeStoredAsTextWideEnoughForItsLongestValue()
    {
        // Act
        var property = EntityProperty(nameof(Entity.Type));

        // Assert
        // PatronInformalGroup is already 19 characters, so the 20 used for the
        // other enums would leave no room at all.
        Assert.Equal(typeof(string), property.GetProviderClrType());
        Assert.Equal(30, property.GetMaxLength());
        Assert.True(
            property.GetMaxLength()
                > Enum.GetNames<EntityType>().Max(x => x.Length));
    }

    [Fact]
    public void Pesel_ShouldBeOptionalAndFlaggedForEncryption()
    {
        // Act
        var property = UserProperty(nameof(User.Pesel));

        // Assert
        // Optional, because a PESEL only appears at the agreement stage and a
        // placeholder in a PESEL column survives every validation. Flagged,
        // because AGENTS.md requires every sensitive field to say so where it is
        // defined, so T-80 misses nothing.
        Assert.True(property.IsNullable);
        Assert.Contains("T-80", property.GetComment() ?? string.Empty);
    }

    [Theory]
    [InlineData(nameof(Entity.Nip))]
    [InlineData(nameof(Entity.Address))]
    public void TypeDependentFields_ShouldBeNullableAndFlaggedForEncryption(
        string propertyName)
    {
        // Act
        var property = EntityProperty(propertyName);

        // Assert
        // NOT NULL on everything was rejected in T-11.2: an entity with no NIP
        // is an informal group, not broken data. The requiredness follows the
        // type and is checked at the API edge.
        Assert.True(property.IsNullable);
        Assert.Contains("T-80", property.GetComment() ?? string.Empty);
    }

    [Fact]
    public void ContactInformation_ShouldBeFlaggedForEncryptionToo()
    {
        // Act
        var property = EntityProperty(nameof(Entity.ContactInformation));

        // Assert
        // Required, unlike Nip and Address, but no less sensitive: for an
        // informal group these are a natural person's contact details. AGENTS.md
        // rule 6 covers every field holding sensitive data, and an unflagged one
        // is exactly what T-80 walks past.
        Assert.False(property.IsNullable);
        Assert.Contains("T-80", property.GetComment() ?? string.Empty);
    }

    [Fact]
    public void Answers_ShouldNotPromiseWholeColumnEncryption()
    {
        // Act
        var comment = TestModel.EntityType<Application>()
            .FindProperty(nameof(Application.Answers))!
            .GetComment();

        // Assert
        // Ciphertext is neither an object nor an array, so encrypting the whole
        // jsonb column would take the check constraint and the searchability
        // with it. The comment has to say the encryption goes inside the
        // document, otherwise T-80 reads a promise the column cannot keep.
        Assert.NotNull(comment);
        Assert.Contains("INSIDE", comment);
    }

    [Fact]
    public void PasswordHash_ShouldSayThatItNeverReachesALog()
    {
        // Act
        var comment = UserProperty(nameof(User.PasswordHash)).GetComment();

        // Assert
        // AGENTS.md security rule 4. A log holding password material is a worse
        // leak than the one the hash defends against.
        Assert.NotNull(comment);
        Assert.Contains("log", comment);
    }

    [Theory]
    [InlineData(nameof(User.CreatedAt))]
    [InlineData(nameof(User.UpdatedAt))]
    [InlineData(nameof(User.DeactivatedAt))]
    public void EveryAccountTimestamp_ShouldBeAnOffsetWithTheUtcConverter(
        string propertyName)
    {
        // Act
        var property = UserProperty(propertyName);

        // Assert
        // A regression. These were DateTime when the class was written, and a
        // DateTime mapped to timestamptz moves the problem to DateTimeKind
        // instead of solving it (docs/architektura.md).
        Assert.Equal(
            typeof(DateTimeOffset),
            Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Theory]
    [InlineData(nameof(Entity.CreatedAt))]
    [InlineData(nameof(Entity.UpdatedAt))]
    [InlineData(nameof(Entity.DeactivatedAt))]
    public void EveryEntityTimestamp_ShouldBeAnOffsetWithTheUtcConverter(
        string propertyName)
    {
        // Act
        var property = EntityProperty(propertyName);

        // Assert
        Assert.Equal(
            typeof(DateTimeOffset),
            Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
        Assert.Equal("timestamp with time zone", property.GetColumnType());
        Assert.IsType<UtcDateTimeOffsetConverter>(property.GetValueConverter());
    }

    [Theory]
    [InlineData(typeof(User), "ck_users_deactivated_at_matches_is_active")]
    [InlineData(typeof(Entity), "ck_entities_deactivated_at_matches_is_active")]
    public void EveryAccountTable_ShouldPairSoftDeleteColumns(
        Type clrType,
        string constraintName)
    {
        // Act
        var constraint = TestModel.Model.FindEntityType(clrType)!
            .GetCheckConstraints()
            .SingleOrDefault(x => x.Name == constraintName);

        // Assert
        // Retention of at least 5 years covers accounts and entities too, so
        // they carry the same soft delete pairing as every other table here.
        Assert.NotNull(constraint);
        Assert.Equal("is_active = (deactivated_at IS NULL)", constraint.Sql);
    }

    [Fact]
    public void UserToEntity_ShouldBeOneToOneOptionalAndWithoutCascade()
    {
        // Arrange
        var navigation = TestModel.EntityType<User>()
            .FindNavigation(nameof(User.Entity));

        // Act
        var foreignKey = navigation!.ForeignKey;

        // Assert
        // The key sits on the account, because an entity exists in its own right
        // while an operator account has no entity at all. Optional for the same
        // reason.
        Assert.Equal(
            nameof(User.EntityId),
            foreignKey.Properties.Single().Name);
        Assert.True(foreignKey.Properties.Single().IsNullable);

        // One to one, which docs/model-danych.md lists as an ASSUMPTION to
        // confirm: we do not know whether several people in one organisation
        // file applications from separate accounts.
        Assert.True(foreignKey.IsUnique);

        // docs/model-danych.md rule 1: zero ON DELETE CASCADE.
        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }
}
