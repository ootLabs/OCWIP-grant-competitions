using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ApplicationStatusHistoryConfiguration
    : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);

        // UUID, not a sequence, same reasoning as Application.Id
        // (docs/model-danych.md rule 3): a row here never appears in a URL of
        // its own, but the convention is one decision for the whole model,
        // not one taken per table.
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // Stored as text, not the enum ordinal, same reasoning as
        // Application.Status: the check constraint below compares against
        // the text, so an ordinal would silently make it meaningless.
        builder.Property(x => x.FromStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ChangedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "When this transition happened, in UTC. Never rewritten: a " +
                "later transition is a new row, not an update to this one.");

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.ToTable(table =>
            // A transition that does not change anything is not a transition;
            // catching it here means a bug that would try to log "Draft to
            // Draft" fails loudly at the database instead of quietly growing
            // an append-only table with rows that mean nothing.
            table.HasCheckConstraint(
                "ck_application_status_history_from_ne_to",
                "from_status <> to_status"));

        // The operator's and the applicant's own read: every transition of
        // one application, in order.
        builder.HasIndex(x => new { x.ApplicationId, x.ChangedAt });

        // NoAction, not Cascade: docs/model-danych.md rule 1. Deactivating an
        // application must not take its own history with it, and retention
        // is at least 5 years for exactly the kind of row this table holds.
        builder.HasOne(x => x.Application)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
