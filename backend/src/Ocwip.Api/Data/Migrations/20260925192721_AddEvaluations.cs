using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_form_definitions_competition_id_version_number",
                table: "form_definitions");

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "form_definitions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Application");

            // Every version published before T-38 is an application form. The
            // default exists only to say so for rows already stored: a later
            // insert that forgets the purpose should fail, not quietly become
            // an application form.
            migrationBuilder.Sql("ALTER TABLE form_definitions ALTER COLUMN purpose DROP DEFAULT;");

            migrationBuilder.AddColumn<Guid>(
                name: "formal_card_definition_id",
                table: "competitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "merit_card_definition_id",
                table: "competitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_applications_competition_id_id",
                table: "applications",
                columns: new[] { "competition_id", "id" });

            migrationBuilder.CreateTable(
                name: "evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluations", x => x.id);
                    table.CheckConstraint("ck_evaluations_answers_is_an_object", "jsonb_typeof(answers) = 'object'");
                    table.CheckConstraint("ck_evaluations_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_evaluations_finished_at_matches_status", "(status = 'Finished') = (finished_at IS NOT NULL)");
                    table.CheckConstraint("ck_evaluations_one_author", "(author_user_id IS NULL) <> (author_name IS NULL)");
                    table.CheckConstraint("ck_evaluations_stage_known", "stage IN ('Formal', 'Merit')");
                    table.CheckConstraint("ck_evaluations_status_known", "status IN ('Draft', 'Finished')");
                    table.ForeignKey(
                        name: "fk_evaluations_applications_competition_id_application_id",
                        columns: x => new { x.competition_id, x.application_id },
                        principalTable: "applications",
                        principalColumns: new[] { "competition_id", "id" });
                    table.ForeignKey(
                        name: "fk_evaluations_asp_net_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_evaluations_asp_net_users_entered_by_user_id",
                        column: x => x.entered_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_evaluations_form_definitions_competition_id_form_definition",
                        columns: x => new { x.competition_id, x.form_definition_id },
                        principalTable: "form_definitions",
                        principalColumns: new[] { "competition_id", "id" });
                });

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_competition_id_purpose_version_number",
                table: "form_definitions",
                columns: new[] { "competition_id", "purpose", "version_number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions",
                sql: "purpose IN ('Application', 'FormalEvaluation', 'MeritEvaluation')");

            migrationBuilder.CreateIndex(
                name: "ix_competitions_id_formal_card_definition_id",
                table: "competitions",
                columns: new[] { "id", "formal_card_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_competitions_id_merit_card_definition_id",
                table: "competitions",
                columns: new[] { "id", "merit_card_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_author_user_id",
                table: "evaluations",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_competition_id_application_id",
                table: "evaluations",
                columns: new[] { "competition_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_competition_id_form_definition_id",
                table: "evaluations",
                columns: new[] { "competition_id", "form_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluations_entered_by_user_id",
                table: "evaluations",
                column: "entered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_evaluations_one_active_formal",
                table: "evaluations",
                column: "application_id",
                unique: true,
                filter: "stage = 'Formal' AND is_active");

            migrationBuilder.CreateIndex(
                name: "ux_evaluations_one_active_merit_per_author",
                table: "evaluations",
                columns: new[] { "application_id", "author_user_id" },
                unique: true,
                filter: "stage = 'Merit' AND is_active AND author_user_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_competitions_form_definitions_id_formal_card_definition_id",
                table: "competitions",
                columns: new[] { "id", "formal_card_definition_id" },
                principalTable: "form_definitions",
                principalColumns: new[] { "competition_id", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_competitions_form_definitions_id_merit_card_definition_id",
                table: "competitions",
                columns: new[] { "id", "merit_card_definition_id" },
                principalTable: "form_definitions",
                principalColumns: new[] { "competition_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refused rather than lossy: dropping the table would hard delete
            // evaluations (rule 5), and once a card is published its version
            // numbers collide with the application form's under the old index.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM evaluations)
                        OR EXISTS (SELECT 1 FROM form_definitions WHERE purpose <> 'Application') THEN
                        RAISE EXCEPTION 'AddEvaluations cannot be reverted: evaluations or evaluation cards exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_competitions_form_definitions_id_formal_card_definition_id",
                table: "competitions");

            migrationBuilder.DropForeignKey(
                name: "fk_competitions_form_definitions_id_merit_card_definition_id",
                table: "competitions");

            migrationBuilder.DropTable(
                name: "evaluations");

            migrationBuilder.DropIndex(
                name: "ix_form_definitions_competition_id_purpose_version_number",
                table: "form_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_form_definitions_purpose_known",
                table: "form_definitions");

            migrationBuilder.DropIndex(
                name: "ix_competitions_id_formal_card_definition_id",
                table: "competitions");

            migrationBuilder.DropIndex(
                name: "ix_competitions_id_merit_card_definition_id",
                table: "competitions");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_applications_competition_id_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "form_definitions");

            migrationBuilder.DropColumn(
                name: "formal_card_definition_id",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "merit_card_definition_id",
                table: "competitions");

            migrationBuilder.CreateIndex(
                name: "ix_form_definitions_competition_id_version_number",
                table: "form_definitions",
                columns: new[] { "competition_id", "version_number" },
                unique: true);
        }
    }
}
