using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_number_matches_status",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_submitted_at_matches_status",
                table: "applications");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "results_approved_at",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the operator approved the results (T-42). Null while the decisions are a draft; set once and never cleared.");

            migrationBuilder.AddColumn<decimal>(
                name: "awarded_grant",
                table: "applications",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                comment: "Grant awarded by the operator (T-42), null for none; a draft until the results are approved.");

            migrationBuilder.AddColumn<string>(
                name: "decision_note",
                table: "applications",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_awarded_grant_positive",
                table: "applications",
                sql: "awarded_grant IS NULL OR awarded_grant > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_number_matches_status",
                table: "applications",
                sql: "(status <> 'Draft') = (number IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_submitted_at_matches_status",
                table: "applications",
                sql: "(status <> 'Draft') = (submitted_at IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_awarded_grant_positive",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_number_matches_status",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_submitted_at_matches_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "results_approved_at",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "awarded_grant",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "decision_note",
                table: "applications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_number_matches_status",
                table: "applications",
                sql: "(status = 'Submitted') = (number IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_submitted_at_matches_status",
                table: "applications",
                sql: "(status = 'Submitted') = (submitted_at IS NOT NULL)");
        }
    }
}
