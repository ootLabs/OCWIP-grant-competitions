using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the schema enforces about the columns T-20 added, against a real
/// PostgreSQL: the continuous intake pairing, the unique competition number
/// and the composite foreign key to the form version in force.
///
/// Separate from CompetitionDatabaseTests for the reason given in
/// docs/testy.md: these are the invariants this card introduces, and putting
/// them next to the ones from T-11.3 makes both files about nothing in
/// particular.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionLifecycleSchemaDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public CompetitionLifecycleSchemaDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_carrying_a_closing_date_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs ciagly z data");
        competition.IsContinuousIntake = true;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // The damaging direction: a competition advertised as never closing
        // quietly closes itself on the date nobody removed.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_end_date_matches_continuous_intake",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_fixed_term_competition_without_a_closing_date_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs terminowy bez daty");
        competition.EndDate = null;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // The other direction, and it is not symmetrical in consequence: this
        // one produces a competition that never closes while claiming a
        // deadline, so nothing in the product would ever stop accepting.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_end_date_matches_continuous_intake",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_without_a_closing_date_is_stored()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs ciagly poprawny");
        competition.IsContinuousIntake = true;
        competition.EndDate = null;

        context.Competitions.Add(competition);

        // Act
        await context.SaveChangesAsync();

        // Assert
        var stored = await context.Competitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Null(stored.EndDate);
        Assert.True(stored.IsContinuousIntake);
    }

    [RequiresDatabaseFact]
    public async Task Two_competitions_cannot_share_a_number()
    {
        // Arrange
        var number = $"1/{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();

        var first = TestCompetition.New("Konkurs z numerem, pierwszy");
        first.Number = number;
        context.Competitions.Add(first);
        await context.SaveChangesAsync();

        var second = TestCompetition.New("Konkurs z numerem, drugi");
        second.Number = number;
        context.Competitions.Add(second);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // The number ends up on an agreement and in correspondence, where two
        // competitions answering to one number is a mess nobody can untangle
        // afterwards.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_competition_does_not_hold_its_number()
    {
        // Arrange
        var number = $"1/{Guid.NewGuid():N}";

        await using var context = _database.CreateContext();

        var mistake = TestCompetition.New("Konkurs z literowka");
        mistake.Number = number;
        mistake.IsActive = false;
        mistake.DeactivatedAt = DateTimeOffset.UtcNow;
        context.Competitions.Add(mistake);
        await context.SaveChangesAsync();

        var replacement = TestCompetition.New("Konkurs poprawny");
        replacement.Number = number;
        context.Competitions.Add(replacement);

        // Act
        await context.SaveChangesAsync();

        // Assert
        // The index is filtered on is_active. Without the filter a number
        // mistyped once and deactivated would be unusable for the five years
        // of the retention period, because soft delete means the row does not
        // go away and an unfiltered index cannot tell it from a live one.
        Assert.NotEqual(Guid.Empty, replacement.Id);
    }

    [RequiresDatabaseFact]
    public async Task A_competition_cannot_point_at_another_competitions_form()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var mine = TestCompetition.New("Konkurs wskazujacy formularz");
        var other = TestCompetition.New("Konkurs z cudzym formularzem");
        context.Competitions.AddRange(mine, other);
        await context.SaveChangesAsync();

        var otherForm = new FormDefinition
        {
            CompetitionId = other.Id,
            VersionNumber = 1,
            Definition = JsonDocument.Parse("""{"sections":[]}""").RootElement.Clone(),
        };

        context.FormDefinitions.Add(otherForm);
        await context.SaveChangesAsync();

        // Act
        mine.FormDefinitionId = otherForm.Id;

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // This is why the key is composite on (id, form_definition_id) against
        // the alternate key on form_definitions rather than a plain reference
        // to its primary key: a single column key accepts this row, and the
        // competition then serves a form written for a different one.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.ForeignKeyViolation, postgres.SqlState);
    }

    [RequiresDatabaseFact]
    public async Task A_competition_can_point_at_its_own_form_version()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs z wlasnym formularzem");
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        var form = new FormDefinition
        {
            CompetitionId = competition.Id,
            VersionNumber = 1,
            Definition = JsonDocument.Parse("""{"sections":[]}""").RootElement.Clone(),
        };

        context.FormDefinitions.Add(form);
        await context.SaveChangesAsync();

        // Act
        competition.FormDefinitionId = form.Id;
        await context.SaveChangesAsync();

        // Assert
        var stored = await context.Competitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Equal(form.Id, stored.FormDefinitionId);
    }
}
