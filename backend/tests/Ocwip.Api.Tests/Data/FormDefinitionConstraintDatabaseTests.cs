using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the schema refuses to store in a form definition, asserted against a
/// real PostgreSQL: a version number that is not a version, and a definition
/// that is not a document.
/// </summary>
public sealed class FormDefinitionConstraintDatabaseTests
    : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _database;

    public FormDefinitionConstraintDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private async Task<Guid> SeedCompetitionAsync(string title)
    {
        await using var context = _database.CreateContext();
        var competition = TestCompetition.New(title);
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();
        return competition.Id;
    }

    private static FormDefinition NewDefinition(
        Guid competitionId,
        int versionNumber,
        string json = """{"sections":[]}""") =>
        new()
        {
            CompetitionId = competitionId,
            VersionNumber = versionNumber,
            Definition = JsonDocument.Parse(json).RootElement.Clone(),
        };

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task A_version_number_that_is_not_positive_is_refused(
        int versionNumber)
    {
        // Arrange
        // Versions count from 1. Zero or -7 is not a different version, it is a
        // bug in whatever produced it, and the same argument already carried
        // max_grant_amount > 0 in this schema.
        var competitionId = await SeedCompetitionAsync(
            $"Konkurs z wersja {versionNumber}");

        await using var context = _database.CreateContext();
        context.FormDefinitions.Add(NewDefinition(competitionId, versionNumber));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_form_definitions_version_number_positive",
            postgres.ConstraintName);
    }

    [Fact]
    public async Task Version_number_one_is_accepted()
    {
        // Arrange
        // The constraint has to leave the first version usable, otherwise it
        // blocks every competition instead of only bad data.
        var competitionId = await SeedCompetitionAsync("Konkurs z wersja 1");

        await using var context = _database.CreateContext();
        var definition = NewDefinition(competitionId, versionNumber: 1);
        context.FormDefinitions.Add(definition);

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal(1, definition.VersionNumber);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("\"Formularz\"")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task A_definition_that_is_not_a_document_is_refused(string json)
    {
        // Arrange
        // A form definition is a document. Without the constraint the column
        // stores 123 just as happily, and the first reader of that row has no
        // way to tell a scalar from a form that lost its contents.
        var competitionId = await SeedCompetitionAsync($"Konkurs ze skalarem {json}");

        await using var context = _database.CreateContext();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO form_definitions
                    (competition_id, version_number, definition, is_active)
                VALUES
                    ({competitionId}, 1, {json}::jsonb, true)
                """));

        // Assert
        Assert.Equal(PostgresAssert.CheckViolation, exception.SqlState);
        Assert.Equal(
            "ck_form_definitions_definition_is_a_document",
            exception.ConstraintName);
    }

    [Theory]
    [InlineData("""{"sections":[]}""")]
    [InlineData("""[{"id":"dane-podmiotu"}]""")]
    public async Task Both_an_object_and_an_array_root_are_accepted(string json)
    {
        // Arrange
        // Which of the two the root is belongs to the T-20 contract, so the
        // constraint must not decide it here.
        var competitionId = await SeedCompetitionAsync($"Konkurs z korzeniem {json[0]}");

        await using var context = _database.CreateContext();
        var definition = NewDefinition(competitionId, versionNumber: 1, json);
        context.FormDefinitions.Add(definition);

        // Act
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = _database.CreateContext();
        var stored = await readContext.FormDefinitions
            .SingleAsync(x => x.Id == definition.Id);

        Assert.Equal(
            json.TrimStart()[0] == '[' ? JsonValueKind.Array : JsonValueKind.Object,
            stored.Definition.ValueKind);
    }
}
