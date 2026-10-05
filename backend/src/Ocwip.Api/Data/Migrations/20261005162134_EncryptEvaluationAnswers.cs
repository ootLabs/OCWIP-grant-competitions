using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncryptEvaluationAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "evaluations",
                type: "jsonb",
                nullable: false,
                comment: "The evaluator's answers. Answers of fields the card marks sensitive are encrypted inside the document (T-47a).",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "evaluations",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "The evaluator's answers. Answers of fields the card marks sensitive are encrypted inside the document (T-47a).");
        }
    }
}
