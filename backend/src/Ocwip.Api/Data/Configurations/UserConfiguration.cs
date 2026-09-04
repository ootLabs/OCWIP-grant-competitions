using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(100);

        // 254 is the longest address RFC 5321 lets through, so anything longer
        // is not an address that could ever receive the verification mail.
        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment(
                "Password hash. Never a password, and never written to a log, " +
                "an error body or an API response.");

        // Stored as text, not as the enum ordinal, for the same reason as
        // CompetitionStatus: reordering the enum would reinterpret every row.
        //
        // Applicant is also the STORE default, and that is not a duplicate of
        // the property initializer on the entity. The supported way of creating
        // an operator is a statement typed against the database, so inserts that
        // never reach the change tracker are a real path here, and one of them
        // omitting the column has to produce the least privileged account rather
        // than a NOT NULL error somebody works around by picking a role.
        builder.Property(x => x.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Role.Applicant);

        // Sensitive Information. 11 fits the plaintext number; T-80 owns
        // widening the column when it decides the ciphertext format, because
        // only that card knows how long the encrypted value is.
        builder.Property(x => x.Pesel)
            .HasMaxLength(11)
            .HasComment(
                "PESEL. Sensitive personal data, encrypted at rest in T-80. " +
                "Null until the agreement stage.");

        builder.Property(x => x.IsVerified)
            .IsRequired();

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
                "Null while the account is active.");

        builder.ToTable(table =>
        {
            // Soft delete is two columns, so nothing may set one without the
            // other. Same constraint as on every other entity here.
            table.HasCheckConstraint(
                "ck_users_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");

            // No other text enum in this schema is constrained to its values,
            // and this one is the exception deliberately. Role is the privilege
            // column, and the supported way of granting an operator is a
            // statement somebody types. Without this, UPDATE users SET
            // role = 'operator' is accepted and leaves an account in no role at
            // all: every authorization check then denies it, correctly, for a
            // reason nobody can see by looking at the row.
            //
            // The price is that a fourth role needs a migration, and that is the
            // point rather than the cost. The values are spelled out instead of
            // generated from the enum so the SQL reads as SQL; a test keeps the
            // two lists in step in both directions.
            table.HasCheckConstraint(
                "ck_users_role_is_known",
                "role IN ('Applicant', 'Operator', 'Reviewer')");
        });

        // In the database, not in application code: two accounts on one address
        // break password reset, and a check done with a SELECT before an INSERT
        // loses the race against a second registration.
        //
        // Case sensitive today. Whether "Adam@x.pl" and "adam@x.pl" are one
        // account is a normalization decision that belongs to registration
        // (T-12.1), and it is listed as an open point in docs/model-danych.md.
        //
        // The index covers EVERY row, including soft deleted ones, and that has
        // a consequence T-12.1 must not discover the hard way: a deactivated
        // account keeps its address forever, so re-registering with it fails on
        // 23505. Security rule 3 forbids answering differently for a taken and
        // a free address, so the honest answer would be an indistinguishable
        // success for an account the person can never use. The supported flow
        // is therefore REACTIVATION, not re-registration. A partial index
        // (WHERE is_active) is the alternative and it is a product decision,
        // not a schema detail: see docs/model-danych.md.
        builder.HasIndex(x => x.Email)
            .IsUnique();

        // One to one, an ASSUMPTION to confirm (docs/model-danych.md). The
        // foreign key sits on the account, because an entity exists in its own
        // right while an operator account has no entity at all.
        //
        // The unique index behind it covers soft deleted rows too, so the same
        // consequence as on the e-mail applies: once an organisation's contact
        // person leaves and their account is deactivated, a second account for
        // that entity is refused and the entity has to be reached by
        // reactivating the old one. Resolved together with the one to one
        // assumption itself, not before.
        //
        // NoAction, not Cascade: docs/model-danych.md rule 1.
        builder.HasOne(x => x.Entity)
            .WithOne(x => x.User)
            .HasForeignKey<User>(x => x.EntityId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
