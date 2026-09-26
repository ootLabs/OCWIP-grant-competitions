using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions");

            migrationBuilder.AddColumn<Guid>(
                name: "report_form_definition_id",
                table: "competitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "The applicant's answers. Holds personal data (contact person, group leader); sensitive."),
                    prefill = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "Values taken from the application when the report was started, restored on every save."),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.CheckConstraint("ck_reports_accepted_at_matches_status", "(status = 'Accepted') = (accepted_at IS NOT NULL)");
                    table.CheckConstraint("ck_reports_answers_is_an_object", "jsonb_typeof(answers) = 'object' AND jsonb_typeof(prefill) = 'object'");
                    table.CheckConstraint("ck_reports_submitted_at_matches_status", "(status = 'Draft') = (submitted_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_reports_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_reports_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_reports_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_reports_form_definitions_form_definition_id",
                        column: x => x.form_definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "report_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_status_history_asp_net_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_report_status_history_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions",
                sql: "purpose IN ('Application', 'FormalEvaluation', 'MeritEvaluation', 'Report')");

            migrationBuilder.CreateIndex(
                name: "ix_competitions_id_report_form_definition_id",
                table: "competitions",
                columns: new[] { "id", "report_form_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_report_status_history_changed_by_user_id",
                table: "report_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_status_history_report_id",
                table: "report_status_history",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_competition_id_status",
                table: "reports",
                columns: new[] { "competition_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_entity_id",
                table: "reports",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_form_definition_id",
                table: "reports",
                column: "form_definition_id");

            migrationBuilder.CreateIndex(
                name: "ux_reports_one_active_per_application",
                table: "reports",
                column: "application_id",
                unique: true,
                filter: "is_active");

            migrationBuilder.AddForeignKey(
                name: "fk_competitions_form_definitions_id_report_form_definition_id",
                table: "competitions",
                columns: new[] { "id", "report_form_definition_id" },
                principalTable: "form_definitions",
                principalColumns: new[] { "competition_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_competitions_form_definitions_id_report_form_definition_id",
                table: "competitions");

            migrationBuilder.DropTable(
                name: "report_status_history");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions");

            migrationBuilder.DropIndex(
                name: "ix_competitions_id_report_form_definition_id",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "report_form_definition_id",
                table: "competitions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions",
                sql: "purpose IN ('Application', 'FormalEvaluation', 'MeritEvaluation')");
        }
    }
}
