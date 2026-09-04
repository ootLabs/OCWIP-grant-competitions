using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before anything is altered, because the collision this looks for
            // is the one thing here that CANNOT be repaired by the migration.
            // Uniqueness moves from the address as written onto its upper cased
            // copy, so a database that legally held "Adam@x.pl" and "adam@x.pl"
            // as two accounts has no shape to land in. Without this, the
            // CreateIndex at the bottom aborts with a bare 23505 naming an
            // index nobody has seen yet, and the operator is left to find the
            // rows by hand. Deciding which of two accounts survives is a
            // product question, not something a migration may guess.
            //
            // Grouped by the SAME expression the backfill below writes, not by
            // upper() alone. Two accents typed the other way round collide only
            // after normalization, so a guard that skipped normalize() would
            // pass the pair through and let the unique index reject it a few
            // statements later, which is the bare 23505 this exists to replace.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    collisions text;
                BEGIN
                    SELECT string_agg(DISTINCT lower(email), ', ')
                      INTO collisions
                      FROM users
                     WHERE upper(normalize(email, NFC)) IN (
                               SELECT upper(normalize(email, NFC))
                                 FROM users
                             GROUP BY upper(normalize(email, NFC))
                               HAVING count(*) > 1);

                    IF collisions IS NOT NULL THEN
                        RAISE EXCEPTION
                            'Cannot make the address unique: these addresses exist more than once, differing only in case or in how an accent is spelled: %. Merge each group into a single account, then run this migration again.',
                            collisions;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.AddColumn<int>(
                name: "access_failed_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Nullable here and NOT NULL further down, the same shape as
            // normalized_email below and for the same reason: a NOT NULL column
            // with a store default gives every existing row the value the
            // default produces, and gen_random_uuid() called once per statement
            // would hand every account the SAME stamp. Both stamps arrive
            // empty, get one value each, and only then become required.
            migrationBuilder.AddColumn<string>(
                name: "concurrency_stamp",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "Whether the address was confirmed by clicking the link from T-12.2. Replaces the former is_verified column.");

            migrationBuilder.AddColumn<bool>(
                name: "lockout_enabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_end",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Hand edited, and this is the reason. EF scaffolded this column as
            // NOT NULL with an empty string default, which gives every existing
            // account the same '' and makes the unique index below fail on the
            // second row with 23505. MigrationTests would not have caught it:
            // it migrates a database it just created, and an empty table has no
            // second row to collide.
            //
            // So the column arrives nullable, gets filled from the address it
            // normalizes, and only then becomes NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_user_name",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "Changing this value ends every session of this account. See the session decision in docs/architektura.md.");

            migrationBuilder.AddColumn<string>(
                name: "user_name",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            // upper(normalize(email, NFC)), because that is what Identity's
            // normalizer does to the same address on the way in: it runs
            // string.Normalize(), whose default form is NFC, and only then
            // upper cases. upper() alone looks equivalent and is not. An
            // address typed with a decomposed accent would land here under a
            // string UserManager never produces, so the unique index would
            // accept it a second time in the composed spelling and the account
            // already in the table would be one nothing can find. normalize()
            // is built into PostgreSQL 13 and later and works off its own
            // Unicode tables, so unlike upper() it does not depend on the
            // database locale. A test pins the two implementations against each
            // other, so this line and UserManager cannot drift apart.
            //
            // email_confirmed is copied from is_verified rather than left on
            // its store default, and that copy is the whole reason is_verified
            // is dropped BELOW this statement instead of at the top of the
            // migration. The two columns hold one fact (UserConfiguration.cs),
            // so dropping the old one first would silently reset every already
            // verified account to unverified: T-12.3 gates sign in on a
            // confirmed address, so those accounts would be locked out and
            // asked to confirm an address they confirmed months ago.
            //
            // No COALESCE on the stamps: both columns were added nullable a few
            // statements above, so they are unconditionally NULL here.
            migrationBuilder.Sql(
                """
                UPDATE users
                   SET normalized_email = upper(normalize(email, NFC)),
                       user_name = email,
                       normalized_user_name = upper(normalize(email, NFC)),
                       email_confirmed = is_verified,
                       security_stamp = gen_random_uuid()::text,
                       concurrency_stamp = gen_random_uuid()::text;
                """);

            migrationBuilder.DropColumn(
                name: "is_verified",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            // Hand edited too, and this is the second reason. Both stamps are
            // NOT NULL with a store default the database can produce on its
            // own: IdentityUser initializes concurrency_stamp in its
            // constructor and security_stamp not at all, so every insert that
            // skips UserManager (scripts/seed.py, the schema tests) would
            // otherwise leave an account whose sessions nothing can end.
            // UserConfiguration.cs carries the full reason.
            migrationBuilder.AlterColumn<string>(
                name: "security_stamp",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValueSql: "gen_random_uuid()::text",
                comment: "Changing this value ends every session of this account. See the session decision in docs/architektura.md.",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "Changing this value ends every session of this account. See the session decision in docs/architektura.md.");

            migrationBuilder.AlterColumn<string>(
                name: "concurrency_stamp",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValueSql: "gen_random_uuid()::text",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // NO ACTION on all three, not the CASCADE Identity asks for:
            // docs/model-danych.md rule 1 allows no cascade anywhere, because
            // retention is at least 5 years and a DELETE that quietly succeeds
            // is the failure that rule exists to prevent. The tables being
            // empty today is not an argument, since a cascade only ever fires
            // on the day somebody deletes an account. AppDbContext.cs sets the
            // same behaviour in the model and a test walks every relationship.
            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_user_name",
                table: "users",
                column: "normalized_user_name");

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not fully reversible, and knowingly so: this migration makes
            // "Adam@x.pl" and "adam@x.pl" one account, so a database that
            // accepted both before it ran cannot exist afterwards. Going back
            // restores a unique index on the address as written, which is
            // narrower than what the data may now contain only in the other
            // direction. Nothing here can recreate a distinction the schema
            // stopped allowing.
            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropIndex(
                name: "ix_users_normalized_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_normalized_user_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "access_failed_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "concurrency_stamp",
                table: "users");

            // Reversed in the same order Up() applied it: the column comes back
            // and takes the confirmation over BEFORE the Identity one is
            // dropped. Rolling back is not a reason to forget which addresses
            // were confirmed.
            migrationBuilder.AddColumn<bool>(
                name: "is_verified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE users
                   SET is_verified = email_confirmed;
                """);

            migrationBuilder.DropColumn(
                name: "email_confirmed",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_end",
                table: "users");

            migrationBuilder.DropColumn(
                name: "normalized_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "normalized_user_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "user_name",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }
    }
}
