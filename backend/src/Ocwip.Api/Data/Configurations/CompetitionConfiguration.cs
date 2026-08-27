using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

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

        builder.Property(x => x.EndDate)
            .IsRequired()
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
        });

        // The public listing filters on both: "competitions open right now" is
        // status = Published and end_date in the future. Status leads, because
        // it is the more selective of the two once archived competitions pile up.
        builder.HasIndex(x => new
        {
            x.Status,
            x.EndDate
        });

        // NoAction, not Cascade: docs/model-danych.md rule 1.
        builder.HasMany(x => x.FormDefinitions)
            .WithOne(x => x.Competition)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
