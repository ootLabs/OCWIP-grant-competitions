using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionExpertConfiguration : IEntityTypeConfiguration<CompetitionExpert>
{
    public void Configure(EntityTypeBuilder<CompetitionExpert> builder)
    {
        builder.ToTable("competition_experts", table =>
        {
            table.HasCheckConstraint(
                "ck_competition_experts_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

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

        // NoAction, docs/model-danych.md rule 1.
        builder.HasOne<Competition>()
            .WithMany()
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AppointedById)
            .OnDelete(DeleteBehavior.NoAction);

        // One live appointment per person and competition.
        builder.HasIndex(x => new { x.CompetitionId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_competition_experts_one_active")
            .HasFilter("is_active");

        // "Is this account an expert anywhere" is asked on every request,
        // by the claims factory.
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_competition_experts_user_id");
    }
}
