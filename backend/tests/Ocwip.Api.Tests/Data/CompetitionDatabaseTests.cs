using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The remaining competition invariants against a real PostgreSQL: the grant
/// amount, how the status is stored, the column widths, and what an insert
/// bypassing EF gets from the schema.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public CompetitionDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Negative_max_grant_amount_is_refused_by_the_database()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs z ujemna kwota");
        competition.MaxGrantAmount = -5000m;
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_max_grant_amount_positive",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Status_is_stored_as_text_so_reordering_the_enum_is_safe()
    {
        // Arrange
        var competition = TestCompetition.New("Konkurs opublikowany");
        competition.Status = CompetitionStatus.Published;

        await using var context = _database.CreateContext();
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        // Act
        var stored = await context.Database
            .SqlQuery<string>(
                $"SELECT status AS \"Value\" FROM competitions WHERE id = {competition.Id}")
            .SingleAsync();

        // Assert
        Assert.Equal(nameof(CompetitionStatus.Published), stored);
    }

    [RequiresDatabaseFact]
    public async Task An_insert_bypassing_ef_still_gets_an_id_and_a_created_at()
    {
        // Arrange
        // The reason ids default to gen_random_uuid() and timestamps to now():
        // a seed, a psql session or future raw SQL never touches the change
        // tracker, and a second such row would collide on the primary key.
        await using var context = _database.CreateContext();

        // Act
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO competitions
                (title, description, start_date, end_date,
                 max_grant_amount, status, is_active)
            VALUES
                ('Konkurs bez EF, pierwszy', NULL,
                 '2026-09-01 08:00:00+00', '2026-09-30 08:00:00+00',
                 5000, 'Draft', true),
                ('Konkurs bez EF, drugi', NULL,
                 '2026-09-01 08:00:00+00', '2026-09-30 08:00:00+00',
                 5000, 'Draft', true)
            """);

        // Assert
        var rows = await context.Competitions
            // Scoped to the rows this test inserted: the database is shared
            // across the whole postgres collection, so counting a whole table
            // would depend on which other tests ran first.
            .Where(x => x.Title.StartsWith("Konkurs bez EF"))
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(Guid.Empty, rows.Select(x => x.Id));
        Assert.Equal(2, rows.Select(x => x.Id).Distinct().Count());
        Assert.All(rows, row => Assert.NotEqual(default(DateTimeOffset), row.CreatedAt));
    }

    [RequiresDatabaseFact]
    public async Task An_announcement_longer_than_the_old_limit_still_fits()
    {
        // Arrange
        // The limit used to be 500 characters, which cannot hold a real
        // announcement body. Guards the value chosen for it.
        var competition = TestCompetition.New("Konkurs z dlugim ogloszeniem");
        competition.Description = new string('a', 10000);

        await using var context = _database.CreateContext();
        context.Competitions.Add(competition);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var storedLength = await context.Database
            .SqlQuery<int>(
                $"SELECT length(description) AS \"Value\" FROM competitions WHERE id = {competition.Id}")
            .SingleAsync();

        Assert.Equal(10000, storedLength);
    }
}
