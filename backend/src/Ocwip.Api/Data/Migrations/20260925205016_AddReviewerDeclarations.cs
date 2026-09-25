using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewerDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reviewer_declarations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    refusal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    declaration_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviewer_declarations", x => x.id);
                    table.CheckConstraint("ck_reviewer_declarations_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_reviewer_declarations_reason_matches_decision", "accepted = (refusal_reason IS NULL) AND (refusal_reason IS NULL OR length(btrim(refusal_reason)) > 0)");
                    table.ForeignKey(
                        name: "fk_reviewer_declarations_asp_net_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_reviewer_declarations_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_reviewer_declarations_reviewer_id",
                table: "reviewer_declarations",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ux_reviewer_declarations_one_active",
                table: "reviewer_declarations",
                columns: new[] { "competition_id", "reviewer_id" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviewer_declarations");
        }
    }
}
