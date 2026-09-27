using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    private static readonly JsonSerializerOptions RepresentativesJson = new(JsonSerializerDefaults.Web);

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

        // Plaintext by decision DZ-2 (docs/runbook/plan-v1.md).
        //
        // Nullable, and deliberately without a constraint tying it to the type.
        // An entity with no NIP is an informal group, not broken data. Type
        // dependent validation sits at the API edge (EntityCardValidator).
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

        builder.Property(x => x.LegalForm)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasComment("Legal form of an organisation card (T-93). Null for an informal group.");

        builder.Property(x => x.LegalFormOther)
            .HasMaxLength(200)
            .HasComment("The legal form's name when legal_form is Other.");

        builder.Property(x => x.Register)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasComment("KRS or another register (T-93).");

        builder.Property(x => x.RegisterNumber)
            .HasMaxLength(100)
            .HasComment("Ten digits in KRS, free text in another register.");

        builder.Property(x => x.Regon)
            .HasMaxLength(14)
            .HasComment("REGON, 9 or 14 digits, optional.");

        // Sensitive Information: often a person's own contact details.
        builder.Property(x => x.CorrespondenceAddress)
            .HasMaxLength(500)
            .HasComment(
                "Correspondence address when it differs from the registered one. " +
                "Sensitive personal data, in scope for encryption at rest in T-47a.");

        builder.Property(x => x.Phone)
            .HasMaxLength(50)
            .HasComment("Phone. Sensitive personal data, in scope for encryption at rest in T-47a.");

        builder.Property(x => x.Email)
            .HasMaxLength(320)
            .HasComment(
                "E-mail of the entity, formerly contact_information. Sensitive " +
                "personal data, in scope for encryption at rest in T-47a.");

        // Sensitive Information: the account the grant is paid into.
        builder.Property(x => x.BankAccount)
            .HasMaxLength(26)
            .HasComment(
                "Bank account (NRB), 26 digits without spaces. Sensitive data, " +
                "in scope for encryption at rest in T-47a.");

        // Sensitive Information: natural persons' names. A list read and
        // written whole with the card, never queried row by row, so jsonb
        // rather than a table of its own.
        builder.Property(x => x.Representatives)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                list => JsonSerializer.Serialize(list, RepresentativesJson),
                json => JsonSerializer.Deserialize<List<EntityRepresentative>>(json, RepresentativesJson) ?? new(),
                new ValueComparer<List<EntityRepresentative>>(
                    (left, right) => left!.SequenceEqual(right!),
                    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list.ToList()))
            .HasComment(
                "People authorised to represent the organisation: first name, last name, " +
                "function. Sensitive personal data, in scope for encryption at rest in T-47a.");

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
