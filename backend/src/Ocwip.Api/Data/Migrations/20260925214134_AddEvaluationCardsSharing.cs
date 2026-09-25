using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationCardsSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "evaluation_cards_shared_at",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the evaluation cards were shared with the applicants (T-41b). Null until then; set once and never cleared.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evaluation_cards_shared_at",
                table: "competitions");
        }
    }
}
