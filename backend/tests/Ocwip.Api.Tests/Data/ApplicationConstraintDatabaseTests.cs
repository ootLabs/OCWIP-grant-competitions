using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the schema refuses to store in an application: a competition that does
/// not own the form definition, a submission that is only half a submission, and
/// answers that are not a document.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationConstraintDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public ApplicationConstraintDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_competition_that_does_not_own_the_form_definition_is_refused()
    {
        // Arrange
        // The invariant the composite foreign key exists for. Both identifiers
        // are valid on their own, so two plain foreign keys would accept this
        // row: an application filed in one competition against another
        // competition's form. Nobody could then say which form it was filled
        // against, which is the exact thing the version pointer protects.
        var own = await TestApplicationChain.SeedAsync(_database, "wlasciwy");
        var other = await TestApplicationChain.SeedAsync(_database, "obcy");

        await using var context = _database.CreateContext();
        var application = TestApplication.Draft(own);
        application.FormDefinitionId = other.FormDefinitionId;
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal("fk_applications_form_definitions", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_submitted_application_without_a_submission_date_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "bez daty zlozenia");

        await using var context = _database.CreateContext();
        var application = TestApplication.Submitted(chain, number: "001");
        application.SubmittedAt = null;
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // A submitted offer nobody can date is unusable in a deadline dispute,
        // and the deadline is the one thing this competition cuts off to the
        // minute.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_applications_submitted_at_matches_status",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_draft_carrying_a_submission_date_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "robocza z data");

        await using var context = _database.CreateContext();
        var application = TestApplication.Draft(chain);
        application.SubmittedAt =
            new DateTimeOffset(2026, 9, 15, 10, 30, 0, TimeSpan.Zero);
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // The pairing has to hold in both directions. A draft with a submission
        // date reads as both unsent and sent, and whichever column a reader
        // trusts, the other one contradicts it.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_applications_submitted_at_matches_status",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_submitted_application_without_a_number_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "bez numeru");

        await using var context = _database.CreateContext();
        var application = TestApplication.Submitted(chain, number: "001");
        application.Number = null;
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_applications_number_matches_status", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_draft_carrying_a_number_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "robocza z numerem");

        await using var context = _database.CreateContext();
        var application = TestApplication.Draft(chain);
        application.Number = "001";
        context.Applications.Add(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // A draft nobody ever submits must not burn a number, otherwise the
        // register has gaps that no operator can explain to an applicant.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_applications_number_matches_status", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Two_applications_with_the_same_number_in_one_competition_are_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "z duplikatem numeru");

        await using (var first = _database.CreateContext())
        {
            first.Applications.Add(TestApplication.Submitted(chain, number: "007"));
            await first.SaveChangesAsync();
        }

        // The first row is already committed on purpose: an index that only
        // worked inside a single batch would still let two operators collide.
        await using var second = _database.CreateContext();
        second.Applications.Add(TestApplication.Submitted(chain, number: "007"));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_applications_competition_id_number", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task The_same_number_in_two_competitions_is_allowed()
    {
        // Arrange
        // The index is scoped to a competition, not global. We do not know
        // OCWIP's numbering scheme, and per competition numbering starting at
        // "001" is the likely one, so a global index would reject correct data.
        var first = await TestApplicationChain.SeedAsync(_database, "numeracja pierwsza");
        var second = await TestApplicationChain.SeedAsync(_database, "numeracja druga");

        await using var context = _database.CreateContext();
        var firstApplication = TestApplication.Submitted(first, number: "001");
        var secondApplication = TestApplication.Submitted(second, number: "001");
        context.Applications.Add(firstApplication);
        context.Applications.Add(secondApplication);

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.NotEqual(firstApplication.Id, secondApplication.Id);
        Assert.Equal(
            2,
            await context.Applications.CountAsync(x =>
                x.Number == "001"
                && (x.CompetitionId == first.CompetitionId
                    || x.CompetitionId == second.CompetitionId)));
    }

    [RequiresDatabaseFact]
    public async Task Many_drafts_without_a_number_are_allowed()
    {
        // Arrange
        // The unique index covers the number, and every draft carries none.
        // PostgreSQL treats NULLs in a unique index as distinct, so this works,
        // but it is worth an assertion: getting it wrong would cap a competition
        // at one draft.
        var chain = await TestApplicationChain.SeedAsync(_database, "z wieloma roboczymi");

        await using var context = _database.CreateContext();
        context.Applications.Add(TestApplication.Draft(chain));
        context.Applications.Add(TestApplication.Draft(chain));
        context.Applications.Add(TestApplication.Draft(chain));

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal(
            3,
            await context.Applications.CountAsync(x =>
                x.CompetitionId == chain.CompetitionId
                && x.Status == ApplicationStatus.Draft));
    }

    [RequiresDatabaseTheory]
    [InlineData("123")]
    [InlineData("\"Odpowiedzi\"")]
    [InlineData("true")]
    [InlineData("null")]
    public async Task Answers_that_are_not_a_document_are_refused(string json)
    {
        // Arrange
        // Raw SQL, not EF: a scalar cannot be expressed as a JsonElement the
        // model would accept, and the point is exactly to cover the write path
        // that never goes through the entity.
        var chain = await TestApplicationChain.SeedAsync(_database, $"ze skalarem {json}");

        await using var context = _database.CreateContext();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO applications
                    (competition_id, entity_id, form_definition_id,
                     answers, status, is_active)
                VALUES
                    ({chain.CompetitionId}, {chain.EntityId},
                     {chain.FormDefinitionId}, {json}::jsonb, 'Draft', true)
                """));

        // Assert
        Assert.Equal(PostgresAssert.CheckViolation, exception.SqlState);
        Assert.Equal("ck_applications_answers_is_a_document", exception.ConstraintName);
    }
}
