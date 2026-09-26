using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    /// <summary>
    /// Column widths of the wizard parameters (T-20a), repeated by
    /// CompetitionRequestValidator for the reason written there.
    /// </summary>
    public const int ExpectedResultsLength = 10000;

    public const int UrlLength = 500;

    public const int MessageLength = 10000;

    public const int PaperAddressLength = 500;

    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // 50 is generous for "1/2026" and still bounded. Unique below.
        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        // The announcement body, not a one line summary: docs/slownik.md and the
        // card call this "treść ogłoszenia". A limit exists so nothing unbounded
        // reaches the row, but it has to fit a real announcement.
        builder.Property(x => x.Description)
            .HasMaxLength(10000);

        // Stored as text, not as the enum ordinal: reordering or inserting a
        // member would silently reinterpret every existing row.
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Truncation to a whole minute lives in the entity setter, not in a
        // converter here: a converter would also truncate the operand of a
        // comparison. See Competition.StartDate.
        builder.Property(x => x.StartDate)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "Competition start date and time stored in UTC, " +
                "truncated to a whole minute.");

        // Nullable, unlike StartDate: a continuous intake has no closing
        // moment at all. Paired with is_continuous_intake by a check
        // constraint below, so neither of the two nonsense combinations
        // (continuous with a date, fixed term without one) can be stored.
        builder.Property(x => x.EndDate)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "Competition closing date and time stored in UTC, " +
                "truncated to a whole minute. " +
                "Submission is rejected at or after this moment. " +
                "UTC is used to avoid ambiguity caused by local time zones " +
                "and daylight saving time changes.");

        builder.Property(x => x.MaxGrantAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment(
                "Maximum grant amount allowed for the competition. " +
                "Used later to validate the application budget.");

        builder.Property(x => x.IsContinuousIntake)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment(
                "True when the intake never closes on its own, " +
                "in which case end_date is null.");

        builder.Property(x => x.PublishedAt)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "When an operator published the competition, in UTC. " +
                "Null while it is still a draft.");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasComment(
                "False marks the row as deleted. Rows are never removed, " +
                "because retention is at least 5 years.");

        // now() covers inserts that bypass the change tracker (seed, psql,
        // future raw SQL), the same reason ids default to gen_random_uuid().
        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.DeactivatedAt)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "When the row was marked inactive, in UTC. " +
                "Null while the competition is active.");

        // Steps 1.2 to 1.6 of the wizard (T-20a). All of them nullable or
        // defaulted, because a competition is written over several sittings:
        // validation does not block moving between steps, and completeness is
        // a question asked at publication.

        builder.Property(x => x.ExpectedResults)
            .HasMaxLength(ExpectedResultsLength);

        builder.Property(x => x.RulesUrl)
            .HasMaxLength(UrlLength);

        builder.Property(x => x.SubmissionNotice)
            .HasMaxLength(MessageLength);

        builder.Property(x => x.SubmissionEmailBody)
            .HasMaxLength(MessageLength);

        builder.Property(x => x.RequiresPaperSubmission)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment(
                "True when a paper copy is required alongside the electronic " +
                "one. There is no separate paper workflow.");

        builder.Property(x => x.PaperSubmissionDeadline)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "Deadline for the paper copy, in UTC, truncated to a whole " +
                "minute. Set exactly when requires_paper_submission is true.");

        builder.Property(x => x.PaperSubmissionAddress)
            .HasMaxLength(PaperAddressLength);

        // date, not timestamptz: the report asks for the day a project may run
        // between, and a day carries no time zone to get wrong.
        builder.Property(x => x.ProjectStartDate)
            .HasColumnType("date");

        builder.Property(x => x.ProjectEndDate)
            .HasColumnType("date");

        builder.Property(x => x.TotalPoolAmount)
            .HasPrecision(18, 2)
            .HasComment(
                "The pool of the competition, shown to applicants for " +
                "information. Not a limit checked against anything.");

        builder.Property(x => x.MinGrantAmount)
            .HasPrecision(18, 2);

        // 5,2 holds 100.00 and two decimals, which is the whole range a
        // percentage has. Wider would only let a nonsense figure be stored.
        builder.Property(x => x.MaxIndirectCostPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.MaxInstitutionalDevelopmentPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.PercentageBasis)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(PercentageBasis.GrantAmount);

        builder.Property(x => x.EvaluatorsPerApplication)
            .IsRequired()
            .HasDefaultValue(2);

        builder.Property(x => x.ScoreAggregation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ScoreAggregation.Sum);

        builder.Property(x => x.MeritThreshold)
            .HasPrecision(7, 2);

        builder.Property(x => x.ThresholdIncludesStrategic)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DivergenceThresholdPercent)
            .HasPrecision(5, 2);

        builder.Property(x => x.EvaluationCardsSharedAt)
            .HasComment(
                "When the evaluation cards were shared with the applicants " +
                "(T-41b). Null until then; set once and never cleared.");

        builder.Property(x => x.ResultsApprovedAt)
            .HasComment(
                "When the operator approved the results (T-42). Null while " +
                "the decisions are a draft; set once and never cleared.");

        builder.Property(x => x.ResultEmailFunded).HasMaxLength(4000);
        builder.Property(x => x.ResultEmailReserve).HasMaxLength(4000);
        builder.Property(x => x.ResultEmailRejected).HasMaxLength(4000);

        builder.Property(x => x.MaxAverageAnnualRevenue)
            .HasPrecision(18, 2)
            .HasComment(
                "Threshold on the average annual revenue of the applicant " +
                "over the last three closed years. Null means no threshold; " +
                "zero is a real value.");

        builder.Property(x => x.PersonalDataProcessedUntil)
            .HasColumnType("date")
            .HasComment(
                "Until when personal data from this competition is processed. " +
                "Goes into the GDPR clause and may not fall earlier than five " +
                "years after the intake closes.");

        builder.Property(x => x.MaxAttachmentSizeInBytes)
            .IsRequired()
            .HasDefaultValue(Competition.DefaultMaxAttachmentSizeInBytes);

        builder.Property(x => x.MaxApplicationSizeInBytes)
            .IsRequired()
            .HasDefaultValue(Competition.DefaultMaxApplicationSizeInBytes);

        // A single ToTable call on purpose: a second one reconfigures the table
        // rather than adding to it, so splitting the constraints is a trap.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_competitions_start_date_before_end_date",
                "start_date < end_date");

            table.HasCheckConstraint(
                "ck_competitions_max_grant_amount_positive",
                "max_grant_amount > 0");

            // AT TIME ZONE 'UTC' on purpose: the two argument date_trunc works
            // in the session time zone, so without it the predicate would depend
            // on who is connected.
            table.HasCheckConstraint(
                "ck_competitions_start_date_whole_minute",
                "date_trunc('minute', start_date AT TIME ZONE 'UTC') "
                + "= start_date AT TIME ZONE 'UTC'");

            // end_date is null exactly when the intake is continuous. Without
            // this the two dependent columns drift apart, and both directions
            // of the drift are damaging: a continuous intake carrying a date
            // closes itself one day, and a fixed term one without a date never
            // closes. Written as equality of two booleans, so neither side can
            // be satisfied by a NULL.
            table.HasCheckConstraint(
                "ck_competitions_end_date_matches_continuous_intake",
                "(end_date IS NULL) = is_continuous_intake");

            table.HasCheckConstraint(
                "ck_competitions_end_date_whole_minute",
                "date_trunc('minute', end_date AT TIME ZONE 'UTC') "
                + "= end_date AT TIME ZONE 'UTC'");

            // Soft delete is two columns, so nothing may set one without the
            // other: is_active = false with no date gives a row nobody can date,
            // and is_active = true with a date reads as both live and deleted.
            // "deactivated_at IS NULL" is never itself NULL, so this constraint
            // can never be satisfied by ignorance.
            table.HasCheckConstraint(
                "ck_competitions_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");

            // The paper switch and its two fields move together, in both
            // directions. A deadline left behind after the switch went off is
            // a date the applicant is held to and nobody meant; a switch on
            // with no address tells them to send documents nowhere.
            table.HasCheckConstraint(
                "ck_competitions_paper_submission_fields_match_switch",
                "requires_paper_submission = (paper_submission_deadline IS NOT NULL) "
                + "AND requires_paper_submission = (paper_submission_address IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_competitions_paper_submission_deadline_whole_minute",
                "date_trunc('minute', paper_submission_deadline AT TIME ZONE 'UTC') "
                + "= paper_submission_deadline AT TIME ZONE 'UTC'");

            // The project frame, in the same shape as the intake window. Equal
            // dates are allowed here and not there: a one day project is a real
            // thing, a zero length intake is not.
            table.HasCheckConstraint(
                "ck_competitions_project_dates_in_order",
                "project_start_date <= project_end_date");

            // Money and percentages. Zero is refused for the amounts, because a
            // pool or a minimum grant of zero means the field was not filled in
            // rather than that there is no money, and null already says that.
            // The revenue threshold is the exception the report names: zero
            // there is a real setting.
            table.HasCheckConstraint(
                "ck_competitions_total_pool_amount_positive",
                "total_pool_amount > 0");

            table.HasCheckConstraint(
                "ck_competitions_min_grant_amount_positive",
                "min_grant_amount > 0");

            table.HasCheckConstraint(
                "ck_competitions_min_grant_amount_within_max",
                "min_grant_amount <= max_grant_amount");

            table.HasCheckConstraint(
                "ck_competitions_max_average_annual_revenue_not_negative",
                "max_average_annual_revenue >= 0");

            table.HasCheckConstraint(
                "ck_competitions_evaluation_settings",
                "evaluators_per_application > 0 "
                + "AND (merit_threshold IS NULL OR merit_threshold >= 0) "
                + "AND (divergence_threshold_percent IS NULL OR divergence_threshold_percent BETWEEN 0 AND 100) "
                + "AND score_aggregation IN ('Sum', 'Average')");
            table.HasCheckConstraint(
                "ck_competitions_percentages_within_range",
                "max_indirect_cost_percent BETWEEN 0 AND 100 "
                + "AND max_institutional_development_percent BETWEEN 0 AND 100");

            table.HasCheckConstraint(
                "ck_competitions_upload_limits_positive",
                "max_attachment_size_in_bytes > 0 "
                + "AND max_application_size_in_bytes >= max_attachment_size_in_bytes");
        });

        // The public listing filters on both: "competitions open right now" is
        // status = Published and end_date in the future. Status leads, because
        // it is the more selective of the two once archived competitions pile up.
        builder.HasIndex(x => new
        {
            x.Status,
            x.EndDate
        });

        // Unique, because the number is how the organisation refers to the
        // competition outside this system, on agreements and in letters.
        //
        // Filtered on is_active, so deactivating a competition gives its
        // number back. Without the filter a competition created with a typo in
        // "1/2026" and then deactivated would hold that number for the five
        // years of the retention period, and the real 1/2026 could never be
        // created: soft delete means the row does not go away, and an
        // unfiltered unique index cannot tell that apart from a live one.
        builder.HasIndex(x => x.Number)
            .IsUnique()
            .HasFilter("is_active");

        // NoAction, not Cascade: docs/model-danych.md rule 1.
        builder.HasMany(x => x.FormDefinitions)
            .WithOne(x => x.Competition)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        // The form version in force, pointed at through the alternate key
        // (competition_id, id) on form_definitions, which is the same trick
        // Application uses and for the same reason: a single column key would
        // let a competition adopt another competition's form, and the row
        // would look perfectly valid. No navigation property, because the
        // reverse direction already exists as FormDefinitions and a second one
        // over the same table invites EF to guess which is which.
        builder.HasOne<FormDefinition>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.Id,
                x.FormDefinitionId
            })
            .HasPrincipalKey(x => new
            {
                x.CompetitionId,
                x.Id
            })
            .OnDelete(DeleteBehavior.NoAction);

        // The two evaluation cards in force (T-38), through the same alternate
        // key, so neither can be another competition's card. That the row
        // pointed at has the right purpose is FormDefinitionService's to keep:
        // a foreign key cannot require a constant in a column of the target.
        builder.HasOne<FormDefinition>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.Id,
                x.FormalCardDefinitionId
            })
            .HasPrincipalKey(x => new
            {
                x.CompetitionId,
                x.Id
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<FormDefinition>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.Id,
                x.MeritCardDefinitionId
            })
            .HasPrincipalKey(x => new
            {
                x.CompetitionId,
                x.Id
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<FormDefinition>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.Id,
                x.ReportFormDefinitionId
            })
            .HasPrincipalKey(x => new
            {
                x.CompetitionId,
                x.Id
            })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
