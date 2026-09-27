using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportCostReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonElement>(
                name: "cost_review",
                table: "reports",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "Costs the operator did not accept, row by row of the report budget, with the reason (T-50b).");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_cost_review_is_an_array",
                table: "reports",
                sql: "jsonb_typeof(cost_review) = 'array'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_cost_review_is_an_array",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "cost_review",
                table: "reports");
        }
    }
}
