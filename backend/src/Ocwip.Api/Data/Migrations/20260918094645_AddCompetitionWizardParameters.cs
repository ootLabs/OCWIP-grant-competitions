using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetitionWizardParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "expected_results",
                table: "competitions",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "max_application_size_in_bytes",
                table: "competitions",
                type: "bigint",
                nullable: false,
                defaultValue: 52428800L);

            migrationBuilder.AddColumn<long>(
                name: "max_attachment_size_in_bytes",
                table: "competitions",
                type: "bigint",
                nullable: false,
                defaultValue: 10485760L);

            migrationBuilder.AddColumn<decimal>(
                name: "max_average_annual_revenue",
                table: "competitions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                comment: "Threshold on the average annual revenue of the applicant over the last three closed years. Null means no threshold; zero is a real value.");

            migrationBuilder.AddColumn<decimal>(
                name: "max_indirect_cost_percent",
                table: "competitions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_institutional_development_percent",
                table: "competitions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_grant_amount",
                table: "competitions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "paper_submission_address",
                table: "competitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paper_submission_deadline",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Deadline for the paper copy, in UTC, truncated to a whole minute. Set exactly when requires_paper_submission is true.");

            migrationBuilder.AddColumn<string>(
                name: "percentage_basis",
                table: "competitions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GrantAmount");

            migrationBuilder.AddColumn<DateOnly>(
                name: "personal_data_processed_until",
                table: "competitions",
                type: "date",
                nullable: true,
                comment: "Until when personal data from this competition is processed. Goes into the GDPR clause and may not fall earlier than five years after the intake closes.");

            migrationBuilder.AddColumn<DateOnly>(
                name: "project_end_date",
                table: "competitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "project_start_date",
                table: "competitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_paper_submission",
                table: "competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "True when a paper copy is required alongside the electronic one. There is no separate paper workflow.");

            migrationBuilder.AddColumn<string>(
                name: "rules_url",
                table: "competitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submission_email_body",
                table: "competitions",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submission_notice",
                table: "competitions",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_pool_amount",
                table: "competitions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                comment: "The pool of the competition, shown to applicants for information. Not a limit checked against anything.");

            migrationBuilder.CreateTable(
                name: "competition_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requirement = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    allowed_formats = table.Column<string[]>(type: "text[]", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_attachments", x => x.id);
                    table.CheckConstraint("ck_competition_attachments_allowed_formats_not_empty", "cardinality(allowed_formats) > 0");
                    table.CheckConstraint("ck_competition_attachments_position_not_negative", "position >= 0");
                    table.ForeignKey(
                        name: "fk_competition_attachments_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "competition_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_contacts", x => x.id);
                    table.CheckConstraint("ck_competition_contacts_position_not_negative", "position >= 0");
                    table.ForeignKey(
                        name: "fk_competition_contacts_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_competition_contacts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "competition_cost_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_cost_categories", x => x.id);
                    table.CheckConstraint("ck_competition_cost_categories_position_not_negative", "position >= 0");
                    table.ForeignKey(
                        name: "fk_competition_cost_categories_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_max_average_annual_revenue_not_negative",
                table: "competitions",
                sql: "max_average_annual_revenue >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_min_grant_amount_positive",
                table: "competitions",
                sql: "min_grant_amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_min_grant_amount_within_max",
                table: "competitions",
                sql: "min_grant_amount <= max_grant_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_paper_submission_deadline_whole_minute",
                table: "competitions",
                sql: "date_trunc('minute', paper_submission_deadline AT TIME ZONE 'UTC') = paper_submission_deadline AT TIME ZONE 'UTC'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_paper_submission_fields_match_switch",
                table: "competitions",
                sql: "requires_paper_submission = (paper_submission_deadline IS NOT NULL) AND requires_paper_submission = (paper_submission_address IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_percentages_within_range",
                table: "competitions",
                sql: "max_indirect_cost_percent BETWEEN 0 AND 100 AND max_institutional_development_percent BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_project_dates_in_order",
                table: "competitions",
                sql: "project_start_date <= project_end_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_total_pool_amount_positive",
                table: "competitions",
                sql: "total_pool_amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_upload_limits_positive",
                table: "competitions",
                sql: "max_attachment_size_in_bytes > 0 AND max_application_size_in_bytes >= max_attachment_size_in_bytes");

            migrationBuilder.CreateIndex(
                name: "ix_competition_attachments_competition_id_position",
                table: "competition_attachments",
                columns: new[] { "competition_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_competition_contacts_competition_id_user_id",
                table: "competition_contacts",
                columns: new[] { "competition_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competition_contacts_user_id",
                table: "competition_contacts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_competition_cost_categories_competition_id_category",
                table: "competition_cost_categories",
                columns: new[] { "competition_id", "category" },
                unique: true);

            // Competitions that existed before this migration carry no cost
            // category rows, and "no rows" reads as a competition whose budget
            // allows nothing. They get the three the 2026 template names, which
            // is also what CompetitionService writes for a competition that
            // does not pick its own (R-12).
            migrationBuilder.Sql(
                """
                INSERT INTO competition_cost_categories
                    (id, competition_id, category, position)
                SELECT gen_random_uuid(), c.id, category.name, category.position
                FROM competitions AS c
                CROSS JOIN (VALUES
                    ('DirectCosts', 0),
                    ('InstitutionalDevelopment', 1),
                    ('IndirectCosts', 2)) AS category(name, position);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_attachments");

            migrationBuilder.DropTable(
                name: "competition_contacts");

            migrationBuilder.DropTable(
                name: "competition_cost_categories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_max_average_annual_revenue_not_negative",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_min_grant_amount_positive",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_min_grant_amount_within_max",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_paper_submission_deadline_whole_minute",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_paper_submission_fields_match_switch",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_percentages_within_range",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_project_dates_in_order",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_total_pool_amount_positive",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_upload_limits_positive",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "expected_results",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "max_application_size_in_bytes",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "max_attachment_size_in_bytes",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "max_average_annual_revenue",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "max_indirect_cost_percent",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "max_institutional_development_percent",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "min_grant_amount",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "paper_submission_address",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "paper_submission_deadline",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "percentage_basis",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "personal_data_processed_until",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "project_end_date",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "project_start_date",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "requires_paper_submission",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "rules_url",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "submission_email_body",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "submission_notice",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "total_pool_amount",
                table: "competitions");
        }
    }
}
