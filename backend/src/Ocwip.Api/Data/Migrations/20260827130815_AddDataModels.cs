using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Competition start date and time stored in UTC, truncated to a whole minute."),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Competition closing date and time stored in UTC, truncated to a whole minute. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes."),
                    max_grant_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Maximum grant amount allowed for the competition. Used later to validate the application budget."),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the competition is active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competitions", x => x.id);
                    table.CheckConstraint("ck_competitions_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_competitions_end_date_whole_minute", "date_trunc('minute', end_date AT TIME ZONE 'UTC') = end_date AT TIME ZONE 'UTC'");
                    table.CheckConstraint("ck_competitions_max_grant_amount_positive", "max_grant_amount > 0");
                    table.CheckConstraint("ck_competitions_start_date_before_end_date", "start_date < end_date");
                    table.CheckConstraint("ck_competitions_start_date_whole_minute", "date_trunc('minute', start_date AT TIME ZONE 'UTC') = start_date AT TIME ZONE 'UTC'");
                });

            migrationBuilder.CreateTable(
                name: "form_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "Form structure stored as JSONB. The contract of this column, meaning how sections, fields and validations are shaped, is deliberately not defined here: it is decided in card T-20."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the row as deleted. Rows are never removed, because retention is at least 5 years."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the row was marked inactive, in UTC. Null while the form definition is active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definitions", x => x.id);
                    table.CheckConstraint("ck_form_definitions_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_form_definitions_definition_is_a_document", "jsonb_typeof(definition) IN ('object', 'array')");
                    table.CheckConstraint("ck_form_definitions_version_number_positive", "version_number > 0");
                    table.ForeignKey(
                        name: "fk_form_definitions_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_competitions_status_end_date",
                table: "competitions",
                columns: new[] { "status", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_competition_id_version_number",
                table: "form_definitions",
                columns: new[] { "competition_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_definitions");

            migrationBuilder.DropTable(
                name: "competitions");
        }
    }
}
