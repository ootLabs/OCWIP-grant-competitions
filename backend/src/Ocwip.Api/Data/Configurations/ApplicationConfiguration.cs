using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.HasKey(x => x.Id);

        // UUID, not a sequence: docs/model-danych.md rule 3. The identifier
        // shows up in a URL, and a sequence tells a competitor how many
        // applications arrived and lets them guess somebody else's.
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // See FormDefinition.Definition for why IsRequired() cannot catch an
        // unset JsonElement: the struct default has ValueKind.Undefined and NOT
        // NULL cannot express the difference. The guard belongs at the API edge,
        // and the check constraint below is the schema level half of it.
        builder.Property(x => x.Answers)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasComment(
                "Answers stored as JSONB, shaped by the form definition this " +
                "application points at. The contract of this column is settled " +
                "together with the definition contract in card T-20. " +
                "Holds personal data, so T-80 has to encrypt the sensitive " +
                "fields INSIDE the document: ciphertext is neither an object " +
                "nor an array, so encrypting the whole column would mean " +
                "dropping both the jsonb type and the check constraint below, " +
                "and with them the searchability jsonb was chosen for.");

        // Stored as text, not as the enum ordinal, same reason as
        // CompetitionStatus. The check constraints below compare against the
        // text, so an ordinal would silently make them meaningless.
        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.SubmittedAt)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "When the application was submitted, in UTC. " +
                "Null while it is a draft.");

        builder.Property(x => x.Number)
            .HasMaxLength(50)
            .HasComment(
                "Application number, assigned at submission and unique within " +
                "one competition. Null while the application is a draft.");

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
                "Null while the application is active.");

        // A single ToTable call on purpose: a second one reconfigures the table
        // rather than adding to it.
        builder.ToTable(table =>
        {
            // Two constraints instead of one covering both columns, so the name
            // PostgreSQL reports says which half is wrong. A single condition
            // over three columns is also weaker: it would let a draft carry a
            // number as long as it carried no date.
            table.HasCheckConstraint(
                "ck_applications_submitted_at_matches_status",
                "(status = 'Submitted') = (submitted_at IS NOT NULL)");

            // A draft must not burn a number. Numbers are what the applicant
            // quotes in correspondence, so a register with gaps left by drafts
            // nobody ever submitted is a register nobody can explain.
            table.HasCheckConstraint(
                "ck_applications_number_matches_status",
                "(status = 'Submitted') = (number IS NOT NULL)");

            // Same reasoning as on form_definitions: a set of answers is a
            // document, and without this the column stores 123 just as happily.
            // Object or array, because which of the two the root is belongs to
            // the T-20 contract.
            table.HasCheckConstraint(
                "ck_applications_answers_is_a_document",
                "jsonb_typeof(answers) IN ('object', 'array')");

            table.HasCheckConstraint(
                "ck_applications_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
        });

        // Unique within one competition, not globally. We do not know OCWIP's
        // numbering scheme, and a global index would reject "001" in a second
        // competition, which is correct data under a per competition scheme.
        // This scope rejects nothing a global scheme would produce, because a
        // globally unique number is also unique inside a competition.
        //
        // NULLs count as distinct in a PostgreSQL unique index, so this index
        // does not stand in the way of many drafts, which all carry no number.
        //
        // The index covers EVERY row, including soft deleted ones, exactly like
        // the unique index on users.email. A number, once assigned, therefore
        // never returns to the pool: rule 1 in docs/model-danych.md forbids hard
        // deletes, so a withdrawn application keeps its number and re-filing it
        // in the same competition fails on 23505. That is an ASSUMPTION, listed
        // in docs/model-danych.md; if numbers are meant to be reusable, this
        // becomes a partial index (WHERE is_active).
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.Number
        })
        .IsUnique();

        // The operator's working view: applications in one competition, split by
        // whether they are still drafts.
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.Status
        });

        // NO unique index on (EntityId, CompetitionId), and that absence is a
        // requirement, not an omission. One entity may file several offers in
        // one competition; the client said so in as many words: "tam sie nic nie
        // blokuje, ze organizacja zlozyla oferte i dala druga". A test asserts
        // the absence, because a comment would not survive the next person
        // adding "the obvious missing constraint".
        //
        // NoAction, not Cascade: docs/model-danych.md rule 1. Marking a
        // competition inactive must not take its applications with it.
        builder.HasOne(x => x.Competition)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Entity)
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.EntityId)
            .OnDelete(DeleteBehavior.NoAction);

        // Composite on purpose, and this is the point of the whole entity.
        //
        // An application carries both competition_id and form_definition_id,
        // while the form definition already belongs to a competition. Two plain
        // foreign keys would let the pair drift: an application filed in
        // competition A against a form belonging to competition B passes both of
        // them and is nonsense. A check constraint cannot express the agreement,
        // because it would need a subquery.
        //
        // Referencing the alternate key (competition_id, id) on form_definitions
        // makes the disagreement impossible to store, declaratively and without
        // a trigger. It also means the foreign key to competitions above is
        // strictly redundant for integrity; it stays for the navigation, because
        // the submission deadline lives on the competition and is read on every
        // save.
        builder.HasOne(x => x.FormDefinition)
            .WithMany(x => x.Applications)
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
            // Named explicitly because the generated name would be 65
            // characters and PostgreSQL truncates identifiers at 63, silently.
            .HasConstraintName("fk_applications_form_definitions")
            .OnDelete(DeleteBehavior.NoAction);
    }
}
