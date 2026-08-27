using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// The competition invariants, asserted against a real PostgreSQL rather than
/// against EF metadata: a check constraint that no reordering of the model can
/// quietly drop, and the UTC contract for timestamps.
/// </summary>
public sealed class CompetitionDatabaseTests
    : IClassFixture<PostgresDatabaseFixture>
{
    private const string CheckViolation = "23514";

    private readonly PostgresDatabaseFixture _database;

    public CompetitionDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    internal static Competition NewCompetition(string title = "Konkurs testowy") =>
        new()
        {
            Title = title,
            Description = "Opis konkursu testowego.",
            StartDate = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero),
            MaxGrantAmount = 5000m,
            Status = CompetitionStatus.Draft,
        };

    private static PostgresException AssertPostgresError(DbUpdateException exception)
    {
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        return postgres;
    }

    [RequiresDatabaseFact]
    public async Task End_date_before_start_date_is_refused_by_the_database()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = NewCompetition("Konkurs zamkniety przed otwarciem");
        competition.StartDate = new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
        competition.EndDate = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = AssertPostgresError(exception);
        Assert.Equal(CheckViolation, postgres.SqlState);
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
        var competition = NewCompetition("Konkurs o zerowej dlugosci");
        competition.StartDate = moment;
        competition.EndDate = moment;
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(CheckViolation, AssertPostgresError(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task Negative_max_grant_amount_is_refused_by_the_database()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = NewCompetition("Konkurs z ujemna kwota");
        competition.MaxGrantAmount = -5000m;
        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = AssertPostgresError(exception);
        Assert.Equal(CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_max_grant_amount_positive",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_non_utc_offset_is_saved_and_read_back_as_utc()
    {
        // Arrange
        // Exactly what a Polish browser sends during summer time. Without the
        // conversion Npgsql refuses the write instead of normalizing it.
        var localStart = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var localEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.FromHours(2));

        var competition = NewCompetition("Konkurs ze strefa +02:00");
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
        var competition = NewCompetition("Konkurs z sekundami");
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
    public async Task A_closed_competition_stops_matching_the_open_window_query()
    {
        // Arrange
        // The regression this locks down: while truncation sat in a value
        // converter, EF truncated the operand of the comparison too, so a
        // 12:00:45 "now" reached the server as 12:00:00. The end dates are whole
        // minutes, so the damage is invisible with a strict ">" and shows up on
        // ">=": a competition closing at 12:00 kept satisfying "EndDate >= now"
        // for another 59 seconds.
        var competition = NewCompetition("Konkurs zamkniety o 12:00");
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

    [RequiresDatabaseFact]
    public async Task Updating_a_competition_moves_updated_at_and_leaves_created_at()
    {
        // Arrange
        // The store default now() fires on INSERT only, so without the stamping
        // in SaveChanges a column named updated_at would report the creation
        // instant for the rest of the row's life.
        var competition = NewCompetition("Konkurs przed edycja");

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
                    ('Konkurs z sekundami z psql',
                     '2026-09-01 08:00:30+00', '2026-09-30 08:00:00+00',
                     5000, 'Draft', true)
                """));

        // Assert
        Assert.Equal(CheckViolation, exception.SqlState);
        Assert.Equal(
            "ck_competitions_start_date_whole_minute",
            exception.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task An_announcement_longer_than_the_old_limit_still_fits()
    {
        // Arrange
        // The limit used to be 500 characters, which cannot hold a real
        // announcement body. Guards the value chosen for it.
        var competition = NewCompetition("Konkurs z dlugim ogloszeniem");
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

    [RequiresDatabaseFact]
    public async Task Deactivating_without_a_date_is_refused_by_the_database()
    {
        // Arrange
        var competition = NewCompetition("Konkurs dezaktywowany bez daty");

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
        var postgres = AssertPostgresError(exception);
        Assert.Equal(CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_deactivated_at_matches_is_active",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task An_active_row_carrying_a_deactivation_date_is_refused()
    {
        // Arrange
        // The other half: a row that reads as both live and deleted.
        var competition = NewCompetition("Konkurs aktywny z data dezaktywacji");
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
            AssertPostgresError(exception).ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Deactivating_both_columns_together_is_accepted()
    {
        // Arrange
        // The pairing has to leave the real soft delete path working, otherwise
        // it just blocks the only sanctioned way of removing a row.
        var competition = NewCompetition("Konkurs poprawnie dezaktywowany");

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
    public async Task Status_is_stored_as_text_so_reordering_the_enum_is_safe()
    {
        // Arrange
        var competition = NewCompetition("Konkurs opublikowany");
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
                ('Konkurs z psql', NULL,
                 '2026-09-01 08:00:00+00', '2026-09-30 08:00:00+00',
                 5000, 'Draft', true),
                ('Drugi konkurs z psql', NULL,
                 '2026-09-01 08:00:00+00', '2026-09-30 08:00:00+00',
                 5000, 'Draft', true)
            """);

        // Assert
        var rows = await context.Competitions
            .Where(x => x.Title.EndsWith("z psql"))
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(Guid.Empty, rows.Select(x => x.Id));
        Assert.Equal(2, rows.Select(x => x.Id).Distinct().Count());
        Assert.All(rows, row => Assert.NotEqual(default(DateTimeOffset), row.CreatedAt));
    }

    [RequiresDatabaseFact]
    public async Task A_new_competition_is_active_and_has_no_deactivation_date()
    {
        // Arrange
        var competition = NewCompetition("Konkurs aktywny");

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
}
