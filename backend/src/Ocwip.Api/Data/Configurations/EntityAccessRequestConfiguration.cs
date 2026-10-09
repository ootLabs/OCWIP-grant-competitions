using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EntityAccessRequestConfiguration : IEntityTypeConfiguration<EntityAccessRequest>
{
    public void Configure(EntityTypeBuilder<EntityAccessRequest> builder)
    {
        builder.ToTable("entity_access_requests", table =>
        {
            // Decided means decided by somebody at some moment; pending means
            // neither.
            table.HasCheckConstraint(
                "ck_entity_access_requests_decision_matches_status",
                "(status = 'Pending') = (decided_at IS NULL AND decided_by_id IS NULL)");
            // The operator's decision carries how it was checked (report step
            // 2.2), the founder's carries nothing.
            table.HasCheckConstraint(
                "ck_entity_access_requests_note_matches_operator",
                "decided_by_operator = (operator_note IS NOT NULL) "
                + "AND (operator_note IS NULL OR length(btrim(operator_note)) > 0)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.DecidedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.OperatorNote)
            .HasMaxLength(1000)
            .HasComment(
                "How an operator checked the request outside the system (T-93a). "
                + "Free text that may name a person.");

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // NoAction, docs/model-danych.md rule 1.
        builder.HasOne(x => x.Entity)
            .WithMany()
            .HasForeignKey(x => x.EntityId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RequesterId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.DecidedById)
            .OnDelete(DeleteBehavior.NoAction);

        // One open request per person and card: asking twice is the same
        // request, not a second notification to the founder.
        builder.HasIndex(x => new { x.EntityId, x.RequesterId })
            .IsUnique()
            .HasDatabaseName("ux_entity_access_requests_one_pending")
            .HasFilter("status = 'Pending'");

        // The operator's list: pending requests by age.
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("ix_entity_access_requests_status_created_at");
    }
}
