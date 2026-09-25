using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Stage)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.AuthorName)
            .HasMaxLength(200);

        builder.Property(x => x.Answers)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.FinishedAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeactivatedAt)
            .HasColumnType("timestamp with time zone");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_evaluations_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
            // A finished card nobody can date cannot be shown to have been
            // finished before the ranking was drawn up.
            table.HasCheckConstraint(
                "ck_evaluations_finished_at_matches_status",
                "(status = 'Finished') = (finished_at IS NOT NULL)");
            // Exactly one author: an account, or a name from a paper card.
            table.HasCheckConstraint(
                "ck_evaluations_one_author",
                "(author_user_id IS NULL) <> (author_name IS NULL)");
            table.HasCheckConstraint(
                "ck_evaluations_stage_known",
                "stage IN ('Formal', 'Merit')");
            table.HasCheckConstraint(
                "ck_evaluations_status_known",
                "status IN ('Draft', 'Finished')");
            table.HasCheckConstraint(
                "ck_evaluations_answers_is_an_object",
                "jsonb_typeof(answers) = 'object'");
        });

        // NoAction everywhere, docs/model-danych.md rule 1.
        builder.HasOne(x => x.Application)
            .WithMany()
            .HasForeignKey(x => new
            {
                x.CompetitionId,
                x.ApplicationId
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
                x.CompetitionId,
                x.FormDefinitionId
            })
            .HasPrincipalKey(x => new
            {
                x.CompetitionId,
                x.Id
            })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.EnteredByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // One formal evaluation per application: the operator's staff do it,
        // one person, one card (report, 5.3).
        builder.HasIndex(x => x.ApplicationId)
            .IsUnique()
            .HasDatabaseName("ux_evaluations_one_active_formal")
            .HasFilter("stage = 'Formal' AND is_active");

        // One merit evaluation per application and expert: a second card by
        // the same person would count their opinion twice.
        builder.HasIndex(x => new
        {
            x.ApplicationId,
            x.AuthorUserId
        })
        .IsUnique()
        .HasDatabaseName("ux_evaluations_one_active_merit_per_author")
        .HasFilter("stage = 'Merit' AND is_active AND author_user_id IS NOT NULL");
    }
}
