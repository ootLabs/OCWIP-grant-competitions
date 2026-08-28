using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The invariants an application introduces, asserted against a real
/// PostgreSQL: what the schema must ALLOW (several offers from one entity), what
/// it must refuse to let disappear (nothing cascades), and a jsonb column that
/// returns what it was given.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public ApplicationDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task One_entity_may_file_several_applications_in_one_competition()
    {
        // Arrange
        // The rule the client stated in as many words: "tam sie nic nie blokuje,
        // ze organizacja zlozyla oferte i dala druga". This is the test that
        // proves the missing unique constraint is a decision, not an oversight.
        var chain = await TestApplicationChain.SeedAsync(_database, "z dwoma ofertami");

        await using var context = _database.CreateContext();
        context.Applications.Add(TestApplication.Submitted(chain, number: "001"));
        context.Applications.Add(TestApplication.Submitted(chain, number: "002"));
        context.Applications.Add(TestApplication.Draft(chain));

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal(
            3,
            await context.Applications.CountAsync(x =>
                x.CompetitionId == chain.CompetitionId
                && x.EntityId == chain.EntityId));
    }

    [RequiresDatabaseFact]
    public async Task Deleting_a_competition_that_has_applications_is_refused()
    {
        // Arrange
        // docs/model-danych.md rule 1. Retention of at least 5 years rules out
        // hard deletes, so an operator "removing" a competition may only mark it
        // inactive. The delete has to fail loudly rather than take the
        // applications with it.
        var chain = await TestApplicationChain.SeedAsync(_database, "do usuniecia");

        await using (var seed = _database.CreateContext())
        {
            seed.Applications.Add(TestApplication.Draft(chain));
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var competition = await context.Competitions
            .SingleAsync(x => x.Id == chain.CompetitionId);
        context.Competitions.Remove(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            PostgresAssert.ForeignKeyViolation,
            PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task Deleting_an_entity_that_has_applications_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "z podmiotem");

        await using (var seed = _database.CreateContext())
        {
            seed.Applications.Add(TestApplication.Draft(chain));
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var entity = await context.Entities.SingleAsync(x => x.Id == chain.EntityId);
        context.Entities.Remove(entity);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            PostgresAssert.ForeignKeyViolation,
            PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task Deleting_a_form_definition_an_application_was_filled_against_is_refused()
    {
        // Arrange
        // The version is what makes an old application renderable at all, so
        // losing it silently is worse than losing the competition row.
        var chain = await TestApplicationChain.SeedAsync(_database, "z wersja formularza");

        await using (var seed = _database.CreateContext())
        {
            seed.Applications.Add(TestApplication.Draft(chain));
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var definition = await context.FormDefinitions
            .SingleAsync(x => x.Id == chain.FormDefinitionId);
        context.FormDefinitions.Remove(definition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            PostgresAssert.ForeignKeyViolation,
            PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task An_application_pointing_at_no_entity_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "bez podmiotu");

        await using var context = _database.CreateContext();
        var application = TestApplication.Draft(chain);
        application.EntityId = Guid.NewGuid();
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("fk_applications_entities_entity_id", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task The_answers_survive_a_jsonb_round_trip()
    {
        // Arrange
        const string json =
            """
            {"strona-1":{"nazwa-zadania":"Warsztaty","kwota":9000.50},"strona-2":{"partnerzy":["A","B"],"uwagi":null,"zgoda":true}}
            """;

        var chain = await TestApplicationChain.SeedAsync(_database, "z odpowiedziami");
        var application = TestApplication.Draft(chain, json);

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Applications.Add(application);
            await writeContext.SaveChangesAsync();
        }

        // Act
        await using var readContext = _database.CreateContext();
        var stored = await readContext.Applications
            .SingleAsync(x => x.Id == application.Id);

        // Assert
        // Compared as a JSON tree, not as text: jsonb keeps no key order and no
        // insignificant whitespace, so a string comparison would fail for the
        // wrong reason.
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(json),
                JsonNode.Parse(stored.Answers.GetRawText())),
            $"jsonb round trip changed the answers: {stored.Answers.GetRawText()}");

        Assert.Equal(
            JsonValueKind.Null,
            stored.Answers.GetProperty("strona-2").GetProperty("uwagi").ValueKind);
    }

    [RequiresDatabaseFact]
    public async Task The_answers_column_is_really_jsonb_and_queryable_as_json()
    {
        // Arrange
        // Proof that the column is jsonb and not text: the -> and ->> operators
        // only work on a JSON type, so this query fails if the type drifts. That
        // matters because docs/architektura.md picks jsonb precisely so the
        // answers can be searched and indexed later.
        var chain = await TestApplicationChain.SeedAsync(_database, "z zapytaniem po jsonb");
        var application = TestApplication.Draft(
            chain,
            """{"strona-1":{"nazwa-zadania":"Warsztaty dla seniorow"}}""");

        await using var context = _database.CreateContext();
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        // Act
        var title = await context.Database
            .SqlQuery<string>(
                $"""
                SELECT answers -> 'strona-1' ->> 'nazwa-zadania' AS "Value"
                FROM applications
                WHERE id = {application.Id}
                """)
            .SingleAsync();

        // Assert
        Assert.Equal("Warsztaty dla seniorow", title);
    }
}
