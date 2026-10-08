using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// T-93a, report decision 7: access to a Podmiot card goes with the
    /// organisation. users.entity_id (one account, one card) becomes
    /// entity_members (many to many, with a founder), requests to join get
    /// their own table, and an active card's NIP becomes unique, which is how
    /// a second person is sent to ask instead of founding a duplicate.
    ///
    /// Not additive, and that is allowed here: the schema freezes at G1
    /// (docs/runbook/plan-v1.md), and every row of users.entity_id is carried
    /// into entity_members before the column goes.
    /// </summary>
    public partial class OrganisationMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refused before anything changes: two active cards with one NIP
            // would fail the unique index halfway through, with a constraint
            // name instead of a reason.
            migrationBuilder.Sql(RefuseDuplicateNips);

            migrationBuilder.CreateTable(
                name: "entity_access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_operator = table.Column<bool>(type: "boolean", nullable: false),
                    operator_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "How an operator checked the request outside the system (T-93a). Free text that may name a person."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entity_access_requests", x => x.id);
                    table.CheckConstraint("ck_entity_access_requests_decision_matches_status", "(status = 'Pending') = (decided_at IS NULL AND decided_by_id IS NULL)");
                    table.CheckConstraint("ck_entity_access_requests_note_matches_operator", "decided_by_operator = (operator_note IS NOT NULL) AND (operator_note IS NULL OR length(btrim(operator_note)) > 0)");
                    table.ForeignKey(
                        name: "fk_entity_access_requests_asp_net_users_decided_by_id",
                        column: x => x.decided_by_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_entity_access_requests_asp_net_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_entity_access_requests_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "entity_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_founder = table.Column<bool>(type: "boolean", nullable: false, comment: "True for the account that created the card and decides who joins it (T-93a)."),
                    access_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entity_members", x => x.id);
                    table.CheckConstraint("ck_entity_members_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_entity_members_founder_has_no_request", "is_founder = (access_request_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_entity_members_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_entity_members_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_entity_members_entity_access_requests_access_request_id",
                        column: x => x.access_request_id,
                        principalTable: "entity_access_requests",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ux_entities_nip_active",
                table: "entities",
                column: "nip",
                unique: true,
                filter: "is_active AND nip IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_entity_access_requests_decided_by_id",
                table: "entity_access_requests",
                column: "decided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_entity_access_requests_requester_id",
                table: "entity_access_requests",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_entity_access_requests_status_created_at",
                table: "entity_access_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_entity_access_requests_one_pending",
                table: "entity_access_requests",
                columns: new[] { "entity_id", "requester_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_entity_members_access_request_id",
                table: "entity_members",
                column: "access_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_entity_members_user_id",
                table: "entity_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_entity_members_one_active",
                table: "entity_members",
                columns: new[] { "entity_id", "user_id" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ux_entity_members_one_founder",
                table: "entity_members",
                column: "entity_id",
                unique: true,
                filter: "is_founder AND is_active");

            // Every account that pointed at a card founded it: under one to
            // one there was nobody else who could have.
            migrationBuilder.Sql(
                """
                INSERT INTO entity_members (entity_id, user_id, is_founder, access_request_id, is_active, created_at, updated_at)
                SELECT u.entity_id, u.id, true, NULL, true, e.created_at, now()
                  FROM users u
                  JOIN entities e ON e.id = u.entity_id
                 WHERE u.entity_id IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_users_entities_entity_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_entity_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One to one cannot hold what many to many may already say. Only
            // the founder fits back into users.entity_id; a founder of two
            // cards does not fit at all, and quietly keeping one of them
            // would lose an organisation, so that refuses.
            migrationBuilder.Sql(RefuseFoundersOfSeveralCards);

            migrationBuilder.AddColumn<Guid>(
                name: "entity_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE users u
                   SET entity_id = m.entity_id
                  FROM entity_members m
                 WHERE m.user_id = u.id AND m.is_founder AND m.is_active;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_users_entity_id",
                table: "users",
                column: "entity_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_entities_entity_id",
                table: "users",
                column: "entity_id",
                principalTable: "entities",
                principalColumn: "id");

            migrationBuilder.DropTable(
                name: "entity_members");

            migrationBuilder.DropTable(
                name: "entity_access_requests");

            migrationBuilder.DropIndex(
                name: "ux_entities_nip_active",
                table: "entities");
        }

        private const string RefuseDuplicateNips =
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM entities
                     WHERE is_active AND nip IS NOT NULL
                     GROUP BY nip HAVING count(*) > 1)
                THEN
                    RAISE EXCEPTION 'OrganisationMembers: two active entities share a NIP; merge or deactivate one before migrating (T-93a)'
                        USING ERRCODE = 'P0001';
                END IF;
            END
            $$;
            """;

        private const string RefuseFoundersOfSeveralCards =
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM entity_members
                     WHERE is_founder AND is_active
                     GROUP BY user_id HAVING count(*) > 1)
                THEN
                    RAISE EXCEPTION 'OrganisationMembers down: an account founded several entities and users.entity_id holds one (T-93a)'
                        USING ERRCODE = 'P0001';
                END IF;
            END
            $$;
            """;
    }
}
