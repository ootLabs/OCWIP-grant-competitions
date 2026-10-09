using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    private static readonly JsonSerializerOptions RepresentativesJson = new(JsonSerializerDefaults.Web);

    private const string RepresentativesPurpose = "entities.representatives";

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
                "the API edge and not by the schema. Plaintext by decision DZ-2: " +
                "only organisations carry one, and an organisation's NIP is public.");

        // Sensitive Information, encrypted (T-47a): for an informal group
        // this is a natural person's address. Every encrypted column is text
        // without a length: the ciphertext is longer than the plaintext, and
        // the plaintext's limit is kept at the API edge (EntityCardValidator).
        builder.Property(x => x.Address)
            .HasConversion(EncryptedStringConverter.For("entities.address"))
            .HasComment(
                "Address. Required for an organisation only, checked at the " +
                "API edge. Sensitive personal data, encrypted (T-47a).");

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

        // Sensitive Information, encrypted (T-47a): often a person's own
        // contact details.
        builder.Property(x => x.CorrespondenceAddress)
            .HasConversion(EncryptedStringConverter.For("entities.correspondence_address"))
            .HasComment(
                "Correspondence address when it differs from the registered one. " +
                "Sensitive personal data, encrypted (T-47a).");

        builder.Property(x => x.Phone)
            .HasConversion(EncryptedStringConverter.For("entities.phone"))
            .HasComment("Phone. Sensitive personal data, encrypted (T-47a).");

        builder.Property(x => x.Email)
            .HasConversion(EncryptedStringConverter.For("entities.email"))
            .HasComment(
                "E-mail of the entity, formerly contact_information. Sensitive " +
                "personal data, encrypted (T-47a).");

        // Sensitive Information, encrypted (T-47a): the account the grant is
        // paid into.
        builder.Property(x => x.BankAccount)
            .HasConversion(EncryptedStringConverter.For("entities.bank_account"))
            .HasComment(
                "Bank account (NRB), 26 digits without spaces. Sensitive data, " +
                "encrypted (T-47a).");

        // Sensitive Information, encrypted (T-47a): natural persons' names.
        // A list read and written whole with the card, never queried row by
        // row, so one encrypted JSON text rather than a table of its own. A
        // row from before T-47a still holds the plain JSON, which Decrypt
        // hands back as it is.
        builder.Property(x => x.Representatives)
            .IsRequired()
            .HasColumnType("text")
            .HasDefaultValueSql("'[]'")
            .HasConversion(
                // An empty list names nobody, so it stays "[]": an informal
                // group's card needs no key to be read or rolled back.
                list => list.Count == 0
                    ? "[]"
                    : FieldEncryption.Cipher.Encrypt(JsonSerializer.Serialize(list, RepresentativesJson), RepresentativesPurpose),
                stored => JsonSerializer.Deserialize<List<EntityRepresentative>>(
                    FieldEncryption.Cipher.Decrypt(stored, RepresentativesPurpose), RepresentativesJson) ?? new(),
                new ValueComparer<List<EntityRepresentative>>(
                    (left, right) => left!.SequenceEqual(right!),
                    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    list => list.ToList()))
            .HasComment(
                "People authorised to represent the organisation: first name, last name, " +
                "function, as encrypted JSON. Sensitive personal data (T-47a).");

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

        // "Rozpoznawanie po NIP-ie" (T-93a, report step 2.2): one active card
        // per NIP, so a second person asks to join instead of founding a
        // duplicate with another bank account. The service checks first to
        // answer kindly; this index is what holds when two people race.
        // Stored digits only (RegistryNumbers.Nip), so formatting cannot slip
        // a duplicate past it.
        builder.HasIndex(x => x.Nip)
            .IsUnique()
            .HasDatabaseName("ux_entities_nip_active")
            .HasFilter("is_active AND nip IS NOT NULL");
    }
}
