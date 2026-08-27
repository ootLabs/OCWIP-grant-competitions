using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Soft delete and the audit timestamps against a real PostgreSQL: a row is
/// never removed, so the flag and the date have to move together, and
/// updated_at has to actually move.
/// </summary>
public sealed class CompetitionLifecycleDatabaseTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _database;

    public CompetitionLifecycleDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_new_competition_is_active_and_has_no_deactivation_date()
    {
        // Arrange
        var competition = TestCompetition.New("Konkurs aktywny");

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Competitions.Add(competition);
            await writeContext.SaveChangesAsync();
        }

        // Act
        await using var readContext = _database.CreateContext();
        var stored = await readContext.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        // Assert
        Assert.True(stored.IsActive);
        Assert.Null(stored.DeactivatedAt);
    }

    [RequiresDatabaseFact]
    public async Task Deactivating_without_a_date_is_refused_by_the_database()
    {
        // Arrange
        var competition = TestCompetition.New("Konkurs dezaktywowany bez daty");

        await using (var seed = _database.CreateContext())
        {
            seed.Competitions.Add(competition);
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var stored = await context.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        // Half of the soft delete: the flag moves, the date does not.
        stored.IsActive = false;

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_deactivated_at_matches_is_active",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task An_active_row_carrying_a_deactivation_date_is_refused()
    {
        // Arrange
        // The other half: a row that reads as both live and deleted.
        var competition = TestCompetition.New("Konkurs aktywny z data dezaktywacji");
        competition.DeactivatedAt = new DateTimeOffset(
            2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

        await using var context = _database.CreateContext();
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            "ck_competitions_deactivated_at_matches_is_active",
            PostgresAssert.Error(exception).ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Deactivating_both_columns_together_is_accepted()
    {
        // Arrange
        // The pairing has to leave the real soft delete path working, otherwise
        // it just blocks the only sanctioned way of removing a row.
        var competition = TestCompetition.New("Konkurs poprawnie dezaktywowany");

        await using (var seed = _database.CreateContext())
        {
            seed.Competitions.Add(competition);
            await seed.SaveChangesAsync();
        }

        var deactivatedAt = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

        await using (var edit = _database.CreateContext())
        {
            var stored = await edit.Competitions
                .SingleAsync(x => x.Id == competition.Id);
            stored.IsActive = false;
            stored.DeactivatedAt = deactivatedAt;

            // Act
            await edit.SaveChangesAsync();
        }

        // Assert
        await using var context = _database.CreateContext();
        var after = await context.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        Assert.False(after.IsActive);
        Assert.Equal(deactivatedAt, after.DeactivatedAt);
    }

    [RequiresDatabaseFact]
    public async Task Updating_a_competition_moves_updated_at_and_leaves_created_at()
    {
        // Arrange
        // The store default now() fires on INSERT only, so without the stamping
        // in SaveChanges a column named updated_at would report the creation
        // instant for the rest of the row's life.
        var competition = TestCompetition.New("Konkurs przed edycja");

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Competitions.Add(competition);
            await writeContext.SaveChangesAsync();
        }

        DateTimeOffset createdAt;
        DateTimeOffset updatedAtBefore;

        await using (var readContext = _database.CreateContext())
        {
            var before = await readContext.Competitions
                .SingleAsync(x => x.Id == competition.Id);
            createdAt = before.CreatedAt;
            updatedAtBefore = before.UpdatedAt;
        }

        // Act
        await using (var editContext = _database.CreateContext())
        {
            var editable = await editContext.Competitions
                .SingleAsync(x => x.Id == competition.Id);
            editable.Title = "Konkurs po edycji";
            editable.CreatedAt = DateTimeOffset.UnixEpoch;
            await editContext.SaveChangesAsync();
        }

        // Assert
        await using var finalContext = _database.CreateContext();
        var after = await finalContext.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Equal("Konkurs po edycji", after.Title);
        Assert.True(
            after.UpdatedAt > updatedAtBefore,
            $"updated_at did not move: {updatedAtBefore:o} then {after.UpdatedAt:o}");

        // An update never rewrites the creation instant, even when the caller
        // sets it on the tracked entity.
        Assert.Equal(createdAt, after.CreatedAt);
    }
}
