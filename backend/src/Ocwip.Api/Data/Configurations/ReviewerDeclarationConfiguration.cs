using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ReviewerDeclarationConfiguration : IEntityTypeConfiguration<ReviewerDeclaration>
{
    public void Configure(EntityTypeBuilder<ReviewerDeclaration> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.RefusalReason)
            .HasMaxLength(1000);

        builder.Property(x => x.DeclarationText)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.DecidedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.DeactivatedAt)
            .HasColumnType("timestamp with time zone");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_reviewer_declarations_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
            // A refusal carries its reason (report: "odmowa wymaga powodu"),
            // an acceptance carries none.
            table.HasCheckConstraint(
                "ck_reviewer_declarations_reason_matches_decision",
                "accepted = (refusal_reason IS NULL) "
                + "AND (refusal_reason IS NULL OR length(btrim(refusal_reason)) > 0)");
        });

        // NoAction, docs/model-danych.md rule 1.
        builder.HasOne<Competition>()
            .WithMany()
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.NoAction);

        // One live decision per expert and competition.
        builder.HasIndex(x => new { x.CompetitionId, x.ReviewerId })
            .IsUnique()
            .HasDatabaseName("ux_reviewer_declarations_one_active")
            .HasFilter("is_active");
    }
}
