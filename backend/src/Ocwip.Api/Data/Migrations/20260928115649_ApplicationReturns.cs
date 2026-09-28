using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sections = table.Column<List<string>>(type: "text[]", nullable: false),
                    unlocks_attachments = table.Column<bool>(type: "boolean", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    returned_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the correction was submitted. Null while the return is open."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_returns", x => x.id);
                    table.CheckConstraint("ck_application_returns_deadline_whole_minute", "date_trunc('minute', deadline AT TIME ZONE 'UTC') = deadline AT TIME ZONE 'UTC'");
                    table.CheckConstraint("ck_application_returns_resolved_after_return", "resolved_at IS NULL OR resolved_at >= returned_at");
                    table.CheckConstraint("ck_application_returns_sections_not_empty", "cardinality(sections) > 0");
                    table.ForeignKey(
                        name: "fk_application_returns_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_application_returns_asp_net_users_returned_by_user_id",
                        column: x => x.returned_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                },
                comment: "Returns of a submitted application for correction (T-103): sections, note, deadline.");

            migrationBuilder.CreateTable(
                name: "application_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "The answers as submitted; answers of sensitive fields encrypted inside the document (T-47a)."),
                    entity_snapshot = table.Column<JsonElement>(type: "jsonb", nullable: true, comment: "The entity card as submitted with this version; sensitive fields encrypted (T-47a)."),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_versions", x => x.id);
                    table.CheckConstraint("ck_application_versions_answers_is_a_document", "jsonb_typeof(answers) IN ('object', 'array')");
                    table.CheckConstraint("ck_application_versions_number_positive", "version_number > 0");
                    table.ForeignKey(
                        name: "fk_application_versions_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_application_versions_form_definitions_form_definition_id",
                        column: x => x.form_definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id");
                },
                comment: "Submitted versions of an application kept when it was returned for correction (T-103). Written once.");

            migrationBuilder.CreateIndex(
                name: "ix_application_returns_returned_by_user_id",
                table: "application_returns",
                column: "returned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_application_returns_one_open",
                table: "application_returns",
                column: "application_id",
                unique: true,
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_application_versions_application_id_version_number",
                table: "application_versions",
                columns: new[] { "application_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_versions_form_definition_id",
                table: "application_versions",
                column: "form_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_returns");

            migrationBuilder.DropTable(
                name: "application_versions");
        }
    }
}
