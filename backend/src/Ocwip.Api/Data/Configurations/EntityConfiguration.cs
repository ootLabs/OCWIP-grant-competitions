using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // 30, not the 20 used for the other enums: PatronInformalGroup is
        // already 19 characters, so 20 would leave no room for a rename.
        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(300);

        // Sensitive Information. For an informal group this is a natural
        // person's e-mail address or phone number, exactly parallel to Address,
        // so AGENTS.md rule 6 covers it too.
        builder.Property(x => x.ContactInformation)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment(
                "Contact details of the entity. For an informal group these " +
                "are a natural person's, so they are sensitive personal data " +
                "and in scope for encryption at rest in T-80, which owns " +
                "checking that 500 still holds the ciphertext.");

        // Sensitive Information, encrypted at rest in T-80.
        //
        // Nullable, and deliberately without a constraint tying it to the type.
        // An entity with no NIP is an informal group, not broken data, and we do
        // not know whether a group under an organisation's patronage quotes the
        // patron's NIP. Type dependent validation sits at the API edge.
        builder.Property(x => x.Nip)
            .HasMaxLength(10)
            .HasComment(
                "NIP, 10 digits. Required for an organisation only, checked at " +
                "the API edge and not by the schema. Sensitive data, " +
                "encrypted at rest in T-80. 10 fits the plaintext number and " +
                "no ciphertext at all, so T-80 owns widening this column; " +
                "without that the first encrypted write fails on 22001.");

        // Sensitive Information: for an informal group this is a natural
        // person's address.
        builder.Property(x => x.Address)
            .HasMaxLength(500)
            .HasComment(
                "Address. Required for an organisation only, checked at the " +
                "API edge. Sensitive personal data, encrypted at rest in T-80, " +
                "which owns checking that 500 still holds the ciphertext.");

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
                "Null while the entity is active.");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_entities_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
        });
    }
}
