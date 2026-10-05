using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReportVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "The answers as submitted; answers of sensitive fields encrypted inside the document (T-47a)."),
                    prefill = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "The values taken from the application when this version was submitted; sensitive ones encrypted (T-47a)."),
                    cost_review = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb", comment: "What the operator did not accept in this version, with the reason (S-35)."),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_versions", x => x.id);
                    table.CheckConstraint("ck_report_versions_answers_is_an_object", "jsonb_typeof(answers) = 'object'");
                    table.CheckConstraint("ck_report_versions_cost_review_is_an_array", "jsonb_typeof(cost_review) = 'array'");
                    table.CheckConstraint("ck_report_versions_number_positive", "version_number > 0");
                    table.CheckConstraint("ck_report_versions_prefill_is_an_object", "jsonb_typeof(prefill) = 'object'");
                    table.ForeignKey(
                        name: "fk_report_versions_form_definitions_form_definition_id",
                        column: x => x.form_definition_id,
                        principalTable: "form_definitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_report_versions_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id");
                },
                comment: "Submitted versions of a report kept when it was returned for correction (S-35). Written once.");

            migrationBuilder.CreateIndex(
                name: "ix_report_versions_form_definition_id",
                table: "report_versions",
                column: "form_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_versions_report_id_version_number",
                table: "report_versions",
                columns: new[] { "report_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_versions");
        }
    }
}
