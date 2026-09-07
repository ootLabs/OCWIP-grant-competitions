using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// 254 is the longest address RFC 5321 lets through, so anything longer is
    /// not an address that could ever receive the verification mail. Identity
    /// sizes its own address columns at 256; ours is the tighter number and it
    /// applies to the normalized copy too, because a normalized address is the
    /// same address in upper case.
    /// </summary>
    private const int EmailLength = 254;

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

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(EmailLength);

        // The address as written, upper cased. This is the column uniqueness
        // stands on, so it cannot be null: an account whose normalized address
        // is missing would collide with nobody, because NULLs do not collide in
        // a unique index. Identity fills it through UserManager; anything
        // inserting an account with raw SQL (scripts/seed.py, the schema tests)
        // has to fill it too, and the NOT NULL is what says so out loud.
        builder.Property(x => x.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(EmailLength);

        // Identity needs a username, we do not: an account is identified by its
        // address (Models/User.cs). It mirrors the address, stays nullable
        // because nothing of ours reads it, and gets no unique index of its own,
        // see the index section below.
        builder.Property(x => x.UserName)
            .HasMaxLength(EmailLength);

        builder.Property(x => x.NormalizedUserName)
            .HasMaxLength(EmailLength);

        // Required in the database although the CLR property is nullable, and
        // that mismatch is deliberate: Identity allows a passwordless account
        // for external sign in providers, which docs/zakres.md rules out. An
        // account here always has a hash.
        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment(
                "Password hash. Never a password, and never written to a log, " +
                "an error body or an API response.");

        // Replaces the IsVerified flag this model used to carry. One fact, one
        // column: the verification flow (T-12.2) writes this one through
        // UserManager, and a second flag would be the one nobody updates.
        //
        // Defaulted in the store as well as in code, for the same reason as the
        // role below: an insert that never reaches EF must land on the
        // unverified value rather than fail.
        builder.Property(x => x.EmailConfirmed)
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment(
                "Whether the address was confirmed by clicking the link from " +
                "T-12.2. Replaces the former is_verified column.");

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
        //
        // Still nullable, and registration still must not ask for it: a PESEL
        // appears at the agreement stage (docs/model-danych.md).
        builder.Property(x => x.Pesel)
            .HasMaxLength(11)
            .HasComment(
                "PESEL. Sensitive personal data, encrypted at rest in T-80. " +
                "Null until the agreement stage.");

        // The reason Identity is here at all rather than a hand rolled hasher.
        // docs/architektura.md requires a logout to end the session server
        // side, and changing this value is what invalidates every cookie an
        // account already handed out.
        //
        // Required, with a store default, and neither half is decoration.
        // IdentityUser does NOT initialize this one (it initializes
        // ConcurrencyStamp and nothing else), so every insert that does not go
        // through UserManager arrives without a stamp: scripts/seed.py and the
        // schema tests are both that path. A nullable column would take those
        // rows, and an account with no stamp is an account whose sessions
        // nothing can end, which is the one thing Identity was chosen for.
        // The default is a value the database can produce on its own, for the
        // same reason as the role below: an insert that never reaches EF has to
        // land on a usable row rather than on a NOT NULL error somebody works
        // around by typing a constant.
        builder.Property(x => x.SecurityStamp)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasComment(
                "Changing this value ends every session of this account. " +
                "See the session decision in docs/architektura.md.");

        // Same pair for the same reason, one step further: this one is the
        // concurrency token, so a row that reached the table without it makes
        // every later update compare against NULL. Identity fills it in the
        // constructor, which covers EF, and the store default covers the SQL
        // writers that have no constructor.
        builder.Property(x => x.ConcurrencyStamp)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValueSql("gen_random_uuid()::text")
            .IsConcurrencyToken();

        // Brute force protection, owned by the login card (T-12.3). Enabled by
        // default in the store, not disabled: an account inserted with raw SQL
        // and no opinion on the matter should be protected, not exposed.
        //
        // A store default of true on a bool is the one shape here with a trap in
        // it, so two tests stand on this line. EF omits a property from the
        // INSERT while it still holds its SENTINEL, and a bool inherited from
        // IdentityUser with no initializer starts at false: if the sentinel were
        // that false, then false, the only value worth writing, would be the one
        // value EF drops, and a row would land with lockout enabled while the
        // object that wrote it said otherwise. HasDefaultValue moves the sentinel
        // to the default it sets, so the two stay paired without saying so
        // twice. AccountConfigurationTests pins the pairing and
        // AccountDatabaseTests writes an account with lockout off and reads it
        // back, because that pairing is EF behaviour rather than our decision.
        builder.Property(x => x.LockoutEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.AccessFailedCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.LockoutEnd)
            .HasColumnType("timestamp with time zone");

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

        // Three columns Identity brings that this product has no use for. Left
        // out of the model rather than carried empty, because docs/zakres.md
        // rules out two factor authentication and we never ask for a phone
        // number: a column holding personal data nobody reads is a column
        // nobody protects either.
        //
        // Un-ignoring any of them is a migration, which is the point. A test
        // asserts their absence so this stays a decision rather than a
        // coincidence.
        builder.Ignore(x => x.PhoneNumber);
        builder.Ignore(x => x.PhoneNumberConfirmed);
        builder.Ignore(x => x.TwoFactorEnabled);

        // The table Identity would have called AspNetUsers. Keeping the name we
        // already had is what makes this change an ALTER instead of a second
        // account table beside the first, and it is why scripts/seed.py, the ERD
        // and the schema tests survived the switch: docs/architektura.md.
        builder.ToTable("users", table =>
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
        // On the NORMALIZED column, which settles a question the schema used to
        // leave open: "Adam@x.pl" and "adam@x.pl" are ONE account. Uniqueness
        // used to sit on the address as written, so the two were two accounts
        // and password reset was ambiguous between them. The normalized value is
        // produced by Identity's normalizer in .NET and by
        // upper(normalize(email, NFC)) in SQL (the migration, scripts/seed.py),
        // and a test pins those two against each other.
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
        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_users_normalized_email");

        // Identity makes this one unique. We make it not, because UserName
        // mirrors the address: two unique indexes over the same fact means a
        // duplicate registration fails on whichever the database checks first,
        // and T-12.1 would have to recognise two constraint names to keep one
        // promise. One address, one unique index.
        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique(false)
            .HasDatabaseName("ix_users_normalized_user_name");

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
