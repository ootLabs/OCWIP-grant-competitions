using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the schema enforces about the wizard parameters of steps 1.2 to 1.6
/// (T-20a), against a real PostgreSQL.
///
/// The API edge refuses all of these with a message naming the field, and that
/// is the half a person meets. This is the other half: an insert that never
/// passes through the validator, which is where a seed script, a psql session
/// and any future raw SQL live.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionParametersSchemaDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public CompetitionParametersSchemaDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseTheory]
    // The paper switch off with either field filled in, and on with neither.
    // Both directions damage something: a deadline left behind holds the
    // applicant to a date nobody meant, and a switch with no address tells
    // them to send documents nowhere.
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public async Task The_paper_fields_may_not_drift_away_from_the_switch(
        bool requiresPaper,
        bool carriesDeadline,
        bool carriesAddress)
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs papierowy");
        competition.RequiresPaperSubmission = requiresPaper;
        competition.PaperSubmissionDeadline = carriesDeadline
            ? new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero)
            : null;
        competition.PaperSubmissionAddress = carriesAddress
            ? "ul. Damrota 4, 45-064 Opole"
            : null;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_paper_submission_fields_match_switch",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_minimum_grant_above_the_maximum_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // A competition whose smallest grant is larger than its largest is one
        // no application can ever satisfy, and the applicant meets it as a
        // budget refused by both checks at once.
        var competition = TestCompetition.New("Konkurs z odwrotnymi kwotami");
        competition.MaxGrantAmount = 5000m;
        competition.MinGrantAmount = 6000m;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_min_grant_amount_within_max",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_percentage_outside_zero_to_one_hundred_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs ze zlym procentem");
        competition.MaxIndirectCostPercent = 101m;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_percentages_within_range",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_revenue_threshold_of_zero_is_a_setting_and_not_an_empty_field()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // The one amount where zero means something: the report asks for a
        // field that accepts 0, unlike the pool and the minimum grant, where
        // zero is an unfilled form and null already says that.
        var competition = TestCompetition.New("Konkurs z progiem zero");
        competition.MaxAverageAnnualRevenue = 0m;

        context.Competitions.Add(competition);

        // Act
        await context.SaveChangesAsync();

        // Assert
        await using var reader = _database.CreateContext();
        var stored = await reader.Competitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == competition.Id);

        Assert.Equal(0m, stored.MaxAverageAnnualRevenue);
    }

    [RequiresDatabaseFact]
    public async Task A_project_frame_ending_before_it_starts_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs z odwrotnymi ramami");
        competition.ProjectStartDate = new DateOnly(2027, 1, 1);
        competition.ProjectEndDate = new DateOnly(2026, 12, 31);

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_project_dates_in_order",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_one_day_project_frame_is_allowed()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Equal dates pass here and not in the intake window: a one day
        // project is a real thing, a zero length intake is not.
        var competition = TestCompetition.New("Konkurs na jeden dzien");
        competition.ProjectStartDate = new DateOnly(2027, 1, 1);
        competition.ProjectEndDate = new DateOnly(2027, 1, 1);

        context.Competitions.Add(competition);

        // Act, Assert
        await context.SaveChangesAsync();
    }

    [RequiresDatabaseFact]
    public async Task A_per_file_limit_above_the_per_application_one_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // A limit that can never be used: the applicant meets it as a file
        // accepted by one check and refused by the next.
        var competition = TestCompetition.New("Konkurs ze zlymi limitami");
        competition.MaxAttachmentSizeInBytes = 60L * 1024 * 1024;

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competitions_upload_limits_positive",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task An_attachment_with_no_allowed_format_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs bez formatow");
        competition.Attachments.Add(new CompetitionAttachment
        {
            Title = "Odpis z rejestru",
            Requirement = AttachmentRequirement.Required,
            AllowedFormats = [],
        });

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // An attachment nobody may hand in is a row that only confuses the
        // applicant, and an empty array is what a client sending "[]" produces
        // by accident.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_competition_attachments_allowed_formats_not_empty",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task The_allowed_formats_are_stored_as_text_not_as_ordinals()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var competition = TestCompetition.New("Konkurs z formatami");
        competition.Attachments.Add(new CompetitionAttachment
        {
            Title = "Sprawozdanie finansowe",
            Requirement = AttachmentRequirement.Required,
            AllowedFormats = [AllowedFileFormat.Pdf, AllowedFileFormat.Ods],
        });

        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        // Act
        await using var reader = _database.CreateContext();
        var stored = await reader.Database
            .SqlQuery<string>(
                $"SELECT allowed_formats::text AS \"Value\" FROM competition_attachments WHERE competition_id = {competition.Id}")
            .SingleAsync();

        // Assert
        // Reordering the enum would otherwise reinterpret every stored row,
        // the same reason the competition status is text.
        Assert.Equal("{Pdf,Ods}", stored);
    }

    [RequiresDatabaseFact]
    public async Task One_cost_category_appears_at_most_once_per_competition()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Two rows for one category would turn "is this category on" into a
        // question with a count in the answer.
        var competition = TestCompetition.New("Konkurs z powtorzona kategoria");
        competition.CostCategories.Add(new CompetitionCostCategory
        {
            Category = CostCategory.DirectCosts,
            Position = 0,
        });
        competition.CostCategories.Add(new CompetitionCostCategory
        {
            Category = CostCategory.DirectCosts,
            Position = 1,
        });

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal(
            "ix_competition_cost_categories_competition_id_category",
            postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task One_person_is_a_contact_of_a_competition_at_most_once()
    {
        // Arrange
        await using var context = _database.CreateContext();

        var staff = TestUser.New($"kontakt-{Guid.NewGuid():N}@example.org", Role.Operator);
        context.Users.Add(staff);
        await context.SaveChangesAsync();

        var competition = TestCompetition.New("Konkurs z powtorzonym kontaktem");
        competition.Contacts.Add(new CompetitionContact
        {
            UserId = staff.Id,
            Position = 0,
        });
        competition.Contacts.Add(new CompetitionContact
        {
            UserId = staff.Id,
            Position = 1,
        });

        context.Competitions.Add(competition);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        // A duplicated contact reaches applicants as the same person listed
        // twice on a public page.
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal(
            "ix_competition_contacts_competition_id_user_id",
            postgres.ConstraintName);
    }
}
