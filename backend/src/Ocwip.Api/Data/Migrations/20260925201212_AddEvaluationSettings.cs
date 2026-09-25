using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "divergence_threshold_percent",
                table: "competitions",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "evaluators_per_application",
                table: "competitions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "merit_threshold",
                table: "competitions",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "score_aggregation",
                table: "competitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sum");

            migrationBuilder.AddColumn<bool>(
                name: "threshold_includes_strategic",
                table: "competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_evaluation_settings",
                table: "competitions",
                sql: "evaluators_per_application > 0 AND (merit_threshold IS NULL OR merit_threshold >= 0) AND (divergence_threshold_percent IS NULL OR divergence_threshold_percent BETWEEN 0 AND 100) AND score_aggregation IN ('Sum', 'Average')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_evaluation_settings",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "divergence_threshold_percent",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "evaluators_per_application",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "merit_threshold",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "score_aggregation",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "threshold_includes_strategic",
                table: "competitions");
        }
    }
}
