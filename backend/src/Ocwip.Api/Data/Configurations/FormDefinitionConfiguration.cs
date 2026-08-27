using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.VersionNumber)
            .IsRequired();

        builder.Property(x => x.Definition)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasComment(
                "Form structure stored as JSONB. " +
                "The contract of this column, meaning how sections, fields and " +
                "validations are shaped, is deliberately not defined here: it " +
                "is decided in card T-20.");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasComment(
                "False marks the row as deleted. Rows are never removed, " +
                "because retention is at least 5 years.");

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
                "Null while the form definition is active.");

        builder.ToTable(table =>
        {
            // Soft delete is two columns, so nothing may set one without the
            // other: is_active = false with no date gives a row nobody can date,
            // and is_active = true with a date reads as both live and deleted.
            // "deactivated_at IS NULL" is never itself NULL, so this constraint
            // can never be satisfied by ignorance.
            table.HasCheckConstraint(
                "ck_form_definitions_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
        });

        // The one real invariant this entity introduces: an operator edits the
        // form during the life of a competition, and two rows claiming the same
        // version for one competition would make it impossible to tell which
        // version an application was filled against.
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.VersionNumber
        })
        .IsUnique();
    }
}
