using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// What the schema itself enforces for an application: the check constraints
/// pairing the status with a submission, the indexes the two working views need,
/// the foreign keys that never cascade, and one constraint that must NOT exist.
/// </summary>
public sealed class ApplicationSchemaConfigurationTests
{
    private static IEntityType GetEntityType() => TestModel.EntityType<Application>();

    [Fact]
    public void ShouldPairSubmittedAtWithTheStatus()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_applications_submitted_at_matches_status");

        // Assert
        // A submitted application with no submission instant cannot be dated,
        // and a draft with one reads as both unsent and sent.
        Assert.NotNull(constraint);
        Assert.Equal(
            "(status = 'Submitted') = (submitted_at IS NOT NULL)",
            constraint.Sql);
    }

    [Fact]
    public void ShouldPairTheNumberWithTheStatus()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_applications_number_matches_status");

        // Assert
        // Numbers are assigned at submission, so a draft nobody ever sends must
        // not burn one and leave a gap in the register.
        Assert.NotNull(constraint);
        Assert.Equal(
            "(status = 'Submitted') = (number IS NOT NULL)",
            constraint.Sql);
    }

    [Fact]
    public void ShouldRequireTheAnswersToBeAJsonDocument()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_applications_answers_is_a_document");

        // Assert
        // Object or array, not one of the two: which root the contract picks is
        // decided in T-20, and rejecting scalars prejudges neither.
        Assert.NotNull(constraint);
        Assert.Equal(
            "jsonb_typeof(answers) IN ('object', 'array')",
            constraint.Sql);
    }

    [Fact]
    public void ShouldHaveSoftDeletePairingCheckConstraint()
    {
        // Act
        var constraint = GetEntityType()
            .GetCheckConstraints()
            .SingleOrDefault(x =>
                x.Name == "ck_applications_deactivated_at_matches_is_active");

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
        // Two separate builder.ToTable calls reconfigure the table instead of
        // adding to it, which silently drops the constraints of the first call.
        Assert.Equal(4, constraints.Count);
    }

    [Fact]
    public void ShouldHaveUniqueIndexOnCompetitionIdAndNumber()
    {
        // Act
        var index = GetEntityType()
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Select(p => p.Name)
                    .SequenceEqual(
                    [
                        nameof(Application.CompetitionId),
                        nameof(Application.Number)
                    ]));

        // Assert
        // Scoped to one competition, not global: we do not know OCWIP's
        // numbering scheme, and a global index would reject "001" in a second
        // competition, which is correct data under per competition numbering.
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal("ix_applications_competition_id_number", index.GetDatabaseName());
    }

    [Fact]
    public void ShouldHaveIndexOnCompetitionIdAndStatus()
    {
        // Act
        var index = GetEntityType()
            .GetIndexes()
            .SingleOrDefault(x =>
                x.Properties.Select(p => p.Name)
                    .SequenceEqual(
                    [
                        nameof(Application.CompetitionId),
                        nameof(Application.Status)
                    ]));

        // Assert
        // The operator's working view: applications in one competition, split by
        // whether they are still drafts.
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void ShouldNotConstrainOneEntityToOneApplicationPerCompetition()
    {
        // Arrange
        // Both halves matter. A unique index is one way to say it, and an
        // alternate key is the other: EF turns HasAlternateKey into
        // AddUniqueConstraint, which never shows up in GetIndexes. This very
        // change uses HasAlternateKey on form_definitions, so the idiom is now
        // in the codebase and a guard scanning only indexes would wave it
        // through.
        var entityType = GetEntityType();

        var uniqueColumnSets = entityType
            .GetIndexes()
            .Where(x => x.IsUnique)
            .Select(x => x.Properties.Select(p => p.Name))
            .Concat(entityType
                .GetKeys()
                .Select(x => x.Properties.Select(p => p.Name)));

        // Act
        var offending = uniqueColumnSets
            .Select(names => names.ToList())
            .Where(names =>
                names.Contains(nameof(Application.EntityId))
                && names.Contains(nameof(Application.CompetitionId)))
            .ToList();

        // Assert
        // The absence is a requirement, not an omission. One entity may file
        // several offers in one competition, said by the client in as many
        // words: "tam sie nic nie blokuje, ze organizacja zlozyla oferte i dala
        // druga". This test exists so the next person who spots "the obvious
        // missing unique constraint" is stopped by a red build.
        Assert.Empty(offending);
    }

    [Theory]
    [InlineData(nameof(Application.Competition))]
    [InlineData(nameof(Application.Entity))]
    [InlineData(nameof(Application.FormDefinition))]
    public void EveryRelationship_ShouldRefuseToCascade(string navigationName)
    {
        // Act
        var navigation = GetEntityType().FindNavigation(navigationName);

        // Assert
        Assert.NotNull(navigation);

        // docs/model-danych.md rule 1: zero ON DELETE CASCADE. Retention of at
        // least 5 years rules out hard deletes, so the delete has to fail loudly
        // rather than take the applications with it.
        Assert.Equal(DeleteBehavior.NoAction, navigation.ForeignKey.DeleteBehavior);
    }

    [Fact]
    public void FormDefinition_ShouldBeReferencedTogetherWithItsCompetition()
    {
        // Arrange
        var navigation = GetEntityType()
            .FindNavigation(nameof(Application.FormDefinition));

        // Act
        var foreignKey = navigation!.ForeignKey;

        // Assert
        // The point of the entity. Two plain foreign keys would let the pair
        // drift: an application filed in competition A against a form belonging
        // to competition B satisfies both of them and is nonsense. A check
        // constraint cannot say it, because it would need a subquery, so the
        // agreement is carried by a composite foreign key instead.
        Assert.Equal(
            [nameof(Application.CompetitionId), nameof(Application.FormDefinitionId)],
            foreignKey.Properties.Select(x => x.Name));

        Assert.Equal(
            [nameof(FormDefinition.CompetitionId), nameof(FormDefinition.Id)],
            foreignKey.PrincipalKey.Properties.Select(x => x.Name));

        // Named explicitly, because the generated name would be 65 characters
        // and PostgreSQL truncates identifiers at 63 without saying so.
        Assert.Equal("fk_applications_form_definitions", foreignKey.GetConstraintName());
        Assert.True(foreignKey.GetConstraintName()!.Length <= 63);
    }

    [Fact]
    public void FormDefinitions_ShouldCarryTheAlternateKeyThatMakesThatPossible()
    {
        // Act
        var alternateKey = TestModel.EntityType<FormDefinition>()
            .GetKeys()
            .SingleOrDefault(x =>
                !x.IsPrimaryKey()
                && x.Properties.Select(p => p.Name)
                    .SequenceEqual(
                    [
                        nameof(FormDefinition.CompetitionId),
                        nameof(FormDefinition.Id)
                    ]));

        // Assert
        // PostgreSQL will not let a foreign key reference a column pair without
        // a unique constraint over exactly that pair, so without this key the
        // composite foreign key above cannot exist.
        Assert.NotNull(alternateKey);
    }
}
