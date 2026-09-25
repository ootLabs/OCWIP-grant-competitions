using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ApplicationAssignmentConfiguration
    : IEntityTypeConfiguration<ApplicationAssignment>
{
    public void Configure(EntityTypeBuilder<ApplicationAssignment> builder)
    {
        builder.HasKey(x => x.Id);

        // UUID, not a sequence, same reasoning as Application.Id
        // (docs/model-danych.md rule 3): this row does not appear in a URL of
        // its own today, but the convention is one decision for the whole
        // model, not one taken per table.
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasComment(
                "False marks the assignment revoked. Rows are never removed, "
                + "because retention is at least 5 years and this row is the "
                + "proof a reviewer once had access.");

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
                "When the assignment was revoked, in UTC. Null while active.");

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "ck_application_assignments_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)"));

        // One row per pair: reassigning after a revoke reactivates this row
        // instead of inserting a second one for the same reviewer and
        // application, so there is never a question of which row is current.
        builder.HasIndex(x => new { x.ApplicationId, x.ReviewerId })
            .IsUnique();

        // What EntityScopedHandler actually asks: is there an active row for
        // this reviewer and this application. Covered by the unique index
        // above for equality on the pair, but the handler filters on
        // IsActive too, so a dedicated index keeps that lookup off a scan
        // of every assignment the reviewer has ever had, revoked or not.
        builder.HasIndex(x => new { x.ReviewerId, x.IsActive });

        // NoAction, not Cascade: docs/model-danych.md rule 1. Deactivating an
        // application or a reviewer's account must not take this row with it,
        // the same reasoning as ApplicationStatusHistoryConfiguration.
        builder.HasOne(x => x.Application)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
