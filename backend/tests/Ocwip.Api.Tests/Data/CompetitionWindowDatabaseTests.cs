using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The competition window against a real PostgreSQL: the order of the two
/// dates, the whole minute rule on both ends, and the UTC contract. These are
/// the invariants the requirement in T-11.3 rests on.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionWindowDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public CompetitionWindowDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task End_date_before_start_date_is_refused_by_the_database()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs zamkniety przed otwarciem");
        competition.StartDate = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
        competition.EndDate = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_start_date_before_end_date",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Equal_start_and_end_date_is_refused_by_the_database()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var moment = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var competition = TestCompetition.New("Konkurs o zerowej dlugosci");
        competition.StartDate = moment;
        competition.EndDate = moment;
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(PostgresAssert.CheckViolation, PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task A_non_utc_offset_is_saved_and_read_back_as_utc()
    {
        // Arrange
        // Exactly what a Polish browser sends during summer time. Without the
        // conversion Npgsql refuses the write instead of normalizing it.
        var localStart = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var localEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.FromHours(2));

        var competition = TestCompetition.New("Konkurs ze strefa +02:00");
        competition.StartDate = localStart;
        competition.EndDate = localEnd;

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Competitions.Add(competition);

            // Act
            await writeContext.SaveChangesAsync();
        }

        // Assert
        await using var readContext = _database.CreateContext();
        var stored = await readContext.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Equal(TimeSpan.Zero, stored.StartDate.Offset);
        Assert.Equal(TimeSpan.Zero, stored.EndDate.Offset);
        Assert.Equal(localStart.UtcDateTime, stored.StartDate.UtcDateTime);
        Assert.Equal(localEnd.UtcDateTime, stored.EndDate.UtcDateTime);

        // 10:00 in Poland during summer time is 08:00 UTC. Asserting the wall
        // clock as well, so a converter that only relabelled the offset without
        // shifting the instant would fail here.
        Assert.Equal(8, stored.StartDate.Hour);
    }

    [RequiresDatabaseFact]
    public async Task Seconds_in_the_competition_window_are_dropped_on_save()
    {
        // Arrange
        // T-11.3: the cutoff is a whole minute. An operator typing the 12:00
        // deadline must not end up with 12:00:59 in the column, because two
        // deadlines rendering identically would then behave differently.
        var competition = TestCompetition.New("Konkurs z sekundami");
        competition.StartDate =
            new DateTimeOffset(2026, 9, 1, 8, 0, 17, TimeSpan.Zero);
        competition.EndDate =
            new DateTimeOffset(2026, 9, 30, 12, 0, 59, 999, TimeSpan.Zero);

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Competitions.Add(competition);

            // Act
            await writeContext.SaveChangesAsync();
        }

        // Assert
        await using var readContext = _database.CreateContext();
        var stored = await readContext.Competitions
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Equal(
            new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
            stored.StartDate);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
            stored.EndDate);
    }

    [RequiresDatabaseFact]
    public async Task An_insert_bypassing_ef_cannot_store_a_partial_minute()
    {
        // Arrange
        // The converter covers EF only, so the whole minute rule also lives in
        // the schema. Same reasoning as gen_random_uuid() on the ids.
        await using var context = _database.CreateContext();

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO competitions
                    (title, start_date, end_date,
                     max_grant_amount, status, is_active)
                VALUES
                    ('Konkurs z sekundami, insert bez EF',
                     '2026-09-01 08:00:30+00', '2026-09-30 08:00:00+00',
                     5000, 'Draft', true)
                """));

        // Assert
        Assert.Equal(PostgresAssert.CheckViolation, exception.SqlState);
        Assert.Equal(
            "ck_competitions_start_date_whole_minute",
            exception.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_closed_competition_stops_matching_the_open_window_query()
    {
        // Arrange
        // The regression this locks down: while truncation sat in a value
        // converter, EF truncated the operand of the comparison too, so a
        // 12:00:45 "now" reached the server as 12:00:00. The end dates are whole
        // minutes, so the damage is invisible with a strict ">" and shows up on
        // ">=": a competition closing at 12:00 kept satisfying "EndDate >= now"
        // for another 59 seconds.
        var competition = TestCompetition.New("Konkurs zamkniety o 12:00");
        competition.StartDate = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        competition.EndDate = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        await using (var writeContext = _database.CreateContext())
        {
            writeContext.Competitions.Add(competition);
            await writeContext.SaveChangesAsync();
        }

        var onTheDeadline = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var wellPastIt = new DateTimeOffset(2026, 9, 30, 12, 0, 45, TimeSpan.Zero);

        await using var context = _database.CreateContext();

        // Act
        var matchesOnTheDeadline = await context.Competitions
            .AnyAsync(x => x.Id == competition.Id && x.EndDate >= onTheDeadline);

        var matchesPastTheDeadline = await context.Competitions
            .AnyAsync(x => x.Id == competition.Id && x.EndDate >= wellPastIt);

        // Assert
        Assert.True(matchesOnTheDeadline);
        Assert.False(
            matchesPastTheDeadline,
            "the seconds of the query operand were dropped before reaching the "
            + "server, so a closed competition still matched");
    }
}
