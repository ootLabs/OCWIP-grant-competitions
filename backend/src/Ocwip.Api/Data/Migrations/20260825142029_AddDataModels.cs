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
                name: "competition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Competition start date and time stored in UTC."),
                    end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Competition closing date and time stored in UTC. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes."),
                    max_grant_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Maximum grant amount allowed for the competition. Used later to validate the application budget."),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "form_definition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<JsonDocument>(type: "jsonb", nullable: false, comment: "Form structure stored as JSONB. The JSON contract, including sections, fields and validations, will be defined separately in a future sprint.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_definition_competition_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competition",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_form_definition_competition_id_version_number",
                table: "form_definition",
                columns: new[] { "competition_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "form_definition");

            migrationBuilder.DropTable(
                name: "competition");
        }
    }
}
