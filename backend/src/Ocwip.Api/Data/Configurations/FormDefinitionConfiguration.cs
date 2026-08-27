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
