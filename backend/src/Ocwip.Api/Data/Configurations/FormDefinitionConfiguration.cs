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

        // IsRequired() here only produces NOT NULL. It cannot catch an unset
        // value, because JsonElement is a struct and default(JsonElement) has
        // ValueKind.Undefined: saving such an entity throws inside the Npgsql
        // serializer with "Operation is not valid due to the current state of
        // the object" and no property name. NOT NULL cannot express the
        // difference either, so the guard belongs at the API edge
        // (docs/konwencje.md) and is owed by the write path that T-20 adds. The
        // check constraint below is the schema level half of it.
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

            // Versions are counted from 1. Zero and negative numbers are not a
            // different version, they are a bug in whatever produced them, and
            // the same argument already carried max_grant_amount > 0.
            table.HasCheckConstraint(
                "ck_form_definitions_version_number_positive",
                "version_number > 0");

            // A form definition is a document, not a scalar: without this the
            // column happily stores 123 or "x". Object or array, because which
            // of the two the root is belongs to the T-20 contract, while
            // rejecting scalars and JSON null prejudges nothing.
            table.HasCheckConstraint(
                "ck_form_definitions_definition_is_a_document",
                "jsonb_typeof(definition) IN ('object', 'array')");
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
