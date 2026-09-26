using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResultNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "result_email_funded",
                table: "competitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_email_rejected",
                table: "competitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_email_reserve",
                table: "competitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "result_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_result_notifications", x => x.id);
                    table.CheckConstraint("ck_result_notifications_attempts_not_negative", "attempts >= 0");
                    table.CheckConstraint("ck_result_notifications_result", "result IN ('Funded', 'Reserve', 'Rejected')");
                    table.ForeignKey(
                        name: "fk_result_notifications_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_result_notifications_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_result_notifications_application_id",
                table: "result_notifications",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_result_notifications_competition_id_sent_at",
                table: "result_notifications",
                columns: new[] { "competition_id", "sent_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "result_notifications");

            migrationBuilder.DropColumn(
                name: "result_email_funded",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "result_email_rejected",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "result_email_reserve",
                table: "competitions");
        }
    }
}
