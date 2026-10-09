using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EntityMemberConfiguration : IEntityTypeConfiguration<EntityMember>
{
    public void Configure(EntityTypeBuilder<EntityMember> builder)
    {
        builder.ToTable("entity_members", table =>
        {
            table.HasCheckConstraint(
                "ck_entity_members_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
            // The founder created the card and needed nobody's approval; every
            // other member came through a request.
            table.HasCheckConstraint(
                "ck_entity_members_founder_has_no_request",
                "is_founder = (access_request_id IS NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.IsFounder)
            .HasComment("True for the account that created the card and decides who joins it (T-93a).");

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
        builder.HasOne(x => x.Entity)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.EntityId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<EntityAccessRequest>()
            .WithMany()
            .HasForeignKey(x => x.AccessRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        // One live membership per person and card. Two would make an approval
        // race into a second row instead of a refused insert.
        builder.HasIndex(x => new { x.EntityId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_entity_members_one_active")
            .HasFilter("is_active");

        // ResourceOwnership asks "which cards may this account act for" on
        // every authorized request.
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_entity_members_user_id");

        // One founder per card.
        builder.HasIndex(x => x.EntityId)
            .IsUnique()
            .HasDatabaseName("ux_entity_members_one_founder")
            .HasFilter("is_founder AND is_active");
    }
}
