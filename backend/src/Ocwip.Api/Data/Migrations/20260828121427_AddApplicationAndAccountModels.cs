using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAndAccountModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_form_definitions_competition_id_id",
                table: "form_definitions",
                columns: new[] { "competition_id", "id" });

            migrationBuilder.CreateTable(
                name: "entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    contact_information = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Contact details of the entity. For an informal group these are a natural person's, so they are sensitive personal data and in scope for encryption at rest in T-80, which owns checking that 500 still holds the ciphertext."),
                    nip = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, comment: "NIP, 10 digits. Required for an organisation only, checked at the API edge and not by the schema. Sensitive data, encrypted at rest in T-80. 10 fits the plaintext number and no ciphertext at all, so T-80 owns widening this column; without that the first encrypted write fails on 22001."),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Address. Required for an organisation only, checked at the API edge. Sensitive personal data, encrypted at rest in T-80, which owns checking that 500 still holds the ciphertext."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the entity is active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entities", x => x.id);
                    table.CheckConstraint("ck_entities_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "Answers stored as JSONB, shaped by the form definition this application points at. The contract of this column is settled together with the definition contract in card T-20. Holds personal data, so T-80 has to encrypt the sensitive fields INSIDE the document: ciphertext is neither an object nor an array, so encrypting the whole column would mean dropping both the jsonb type and the check constraint below, and with them the searchability jsonb was chosen for."),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the application was submitted, in UTC. Null while it is a draft."),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Application number, assigned at submission and unique within one competition. Null while the application is a draft."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the application is active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_applications_answers_is_a_document", "jsonb_typeof(answers) IN ('object', 'array')");
                    table.CheckConstraint("ck_applications_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_applications_number_matches_status", "(status = 'Submitted') = (number IS NOT NULL)");
                    table.CheckConstraint("ck_applications_submitted_at_matches_status", "(status = 'Submitted') = (submitted_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_applications_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_applications_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_applications_form_definitions",
                        columns: x => new { x.competition_id, x.form_definition_id },
                        principalTable: "form_definitions",
                        principalColumns: new[] { "competition_id", "id" });
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Password hash. Never a password, and never written to a log, an error body or an API response."),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pesel = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true, comment: "PESEL. Sensitive personal data, encrypted at rest in T-80. Null until the agreement stage."),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the account is active."),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_users_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, comment: "MIME type as declared by the client. Declared, not verified: whoever accepts the upload in T-32 owns checking that the bytes match, because a client controlled value proves nothing."),
                    size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Where the stored bytes live. Must not be guessable and must not be reachable without the same permission check as the application itself: an attachment is another organisation's document. Physical storage is card T-32."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the attachment is active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_attachments_size_in_bytes_positive", "size_in_bytes > 0");
                    table.ForeignKey(
                        name: "fk_attachments_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_competition_id_form_definition_id",
                table: "applications",
                columns: new[] { "competition_id", "form_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_competition_id_number",
                table: "applications",
                columns: new[] { "competition_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_competition_id_status",
                table: "applications",
                columns: new[] { "competition_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_entity_id",
                table: "applications",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_application_id",
                table: "attachments",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_storage_path",
                table: "attachments",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_entity_id",
                table: "users",
                column: "entity_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "entities");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_form_definitions_competition_id_id",
                table: "form_definitions");
        }
    }
}
