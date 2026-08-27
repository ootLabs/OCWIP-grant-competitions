using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The invariants this entity actually introduces, asserted against a real
/// PostgreSQL: one version number per competition, a foreign key that refuses to
/// let a competition disappear from under its form definitions, and a jsonb
/// column that returns what it was given.
/// </summary>
public sealed class FormDefinitionDatabaseTests
    : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _database;

    public FormDefinitionDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private static JsonElement Json(string json) =>
        // Clone detaches the element from the JsonDocument, so it stays valid
        // after the document is disposed. This is why the column is a
        // JsonElement and not a JsonDocument that EF would never dispose.
        JsonDocument.Parse(json).RootElement.Clone();

    private static FormDefinition NewDefinition(
        Guid competitionId,
        int versionNumber,
        string json = """{"sections":[]}""") =>
        new()
        {
            CompetitionId = competitionId,
            VersionNumber = versionNumber,
            Definition = Json(json),
        };

    private async Task<Guid> SeedCompetitionAsync(string title)
    {
        await using var context = _database.CreateContext();
        var competition = TestCompetition.New(title);
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();
        return competition.Id;
    }

    [RequiresDatabaseFact]
    public async Task Two_definitions_with_the_same_version_for_one_competition_are_refused()
    {
        // Arrange
        var competitionId = await SeedCompetitionAsync("Konkurs z duplikatem wersji");

        await using var context = _database.CreateContext();
        context.FormDefinitions.Add(NewDefinition(competitionId, versionNumber: 1));
        context.FormDefinitions.Add(NewDefinition(competitionId, versionNumber: 1));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal(
            "ix_form_definitions_competition_id_version_number",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_duplicate_version_added_in_a_later_transaction_is_also_refused()
    {
        // Arrange
        // The same invariant, but with the first row already committed: a unique
        // index that only worked inside a single batch would pass the test above
        // and still let two operators collide.
        var competitionId = await SeedCompetitionAsync("Konkurs z wersja dopisana pozniej");

        await using (var first = _database.CreateContext())
        {
            first.FormDefinitions.Add(NewDefinition(competitionId, versionNumber: 3));
            await first.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        second.FormDefinitions.Add(NewDefinition(competitionId, versionNumber: 3));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.SaveChangesAsync());

        // Assert
        Assert.Equal(PostgresAssert.UniqueViolation, PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task The_same_version_number_in_two_competitions_is_allowed()
    {
        // Arrange
        // The index is scoped to a competition, not global: version 1 has to
        // exist in every competition.
        var firstCompetitionId = await SeedCompetitionAsync("Pierwszy konkurs");
        var secondCompetitionId = await SeedCompetitionAsync("Drugi konkurs");

        await using var context = _database.CreateContext();
        context.FormDefinitions.Add(NewDefinition(firstCompetitionId, versionNumber: 1));
        context.FormDefinitions.Add(NewDefinition(secondCompetitionId, versionNumber: 1));

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal(
            2,
            await context.FormDefinitions
                .CountAsync(x => x.VersionNumber == 1
                    && (x.CompetitionId == firstCompetitionId
                        || x.CompetitionId == secondCompetitionId)));
    }

    [RequiresDatabaseFact]
    public async Task A_definition_pointing_at_no_competition_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();
        context.FormDefinitions.Add(
            NewDefinition(Guid.NewGuid(), versionNumber: 1));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(PostgresAssert.ForeignKeyViolation, PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task Deleting_a_competition_that_has_definitions_is_refused()
    {
        // Arrange
        // docs/model-danych.md rule 1: zero ON DELETE CASCADE, because a
        // retention of at least 5 years rules out hard deletes. The delete has
        // to fail loudly rather than take the form definitions with it.
        var competitionId = await SeedCompetitionAsync("Konkurs do usuniecia");

        await using (var seed = _database.CreateContext())
        {
            seed.FormDefinitions.Add(NewDefinition(competitionId, versionNumber: 1));
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var competition = await context.Competitions.SingleAsync(x => x.Id == competitionId);
        context.Competitions.Remove(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(PostgresAssert.ForeignKeyViolation, PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task The_definition_survives_a_jsonb_round_trip()
    {
        // Arrange
        const string json =
            """
            {"sections":[{"id":"dane-podmiotu","fields":[{"name":"nip","required":true}]}],"version":2,"nested":{"list":[1,2,3],"flag":false,"empty":null}}
            """;

        var competitionId = await SeedCompetitionAsync("Konkurs z formularzem");
        var definition = NewDefinition(competitionId, versionNumber: 1, json);

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.FormDefinitions.Add(definition);
            await writeContext.SaveChangesAsync();
        }

        // Act
        await using var readContext = _database.CreateContext();
        var stored = await readContext.FormDefinitions
            .SingleAsync(x => x.Id == definition.Id);

        // Assert
        // Compared as a JSON tree, not as text: jsonb keeps no key order and no
        // insignificant whitespace, so a string comparison would fail for the
        // wrong reason. DeepEquals treats objects as unordered.
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(stored.Definition.GetRawText())),
            $"jsonb round trip changed the document: {stored.Definition.GetRawText()}");

        Assert.Equal(
            "nip",
            stored.Definition
                .GetProperty("sections")[0]
                .GetProperty("fields")[0]
                .GetProperty("name")
                .GetString());

        Assert.Equal(
            JsonValueKind.Null,
            stored.Definition.GetProperty("nested").GetProperty("empty").ValueKind);
    }

    [RequiresDatabaseFact]
    public async Task The_definition_column_is_really_jsonb_and_queryable_as_json()
    {
        // Arrange
        // Proof that the column is jsonb and not text: the ->> operator only
        // works on a JSON type, so this query fails if the column type drifts.
        var competitionId = await SeedCompetitionAsync("Konkurs z zapytaniem po jsonb");
        var definition = NewDefinition(
            competitionId,
            versionNumber: 1,
            """{"title":"Formularz ofertowy"}""");

        await using var context = _database.CreateContext();
        context.FormDefinitions.Add(definition);
        await context.SaveChangesAsync();

        // Act
        var title = await context.Database
            .SqlQuery<string>(
                $"SELECT definition ->> 'title' AS \"Value\" FROM form_definitions WHERE id = {definition.Id}")
            .SingleAsync();

        // Assert
        Assert.Equal("Formularz ofertowy", title);
    }
}
