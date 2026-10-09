using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// R-44: an expert is an appointment to one competition. Everybody who
    /// already evaluates somewhere, through an assignment or a declaration,
    /// is appointed to that competition here, so the access rule that now
    /// asks for an appointment takes nothing away from anybody.
    /// </summary>
    public partial class CompetitionExperts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competition_experts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_experts", x => x.id);
                    table.CheckConstraint("ck_competition_experts_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_competition_experts_asp_net_users_appointed_by_id",
                        column: x => x.appointed_by_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_competition_experts_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_competition_experts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_competition_experts_appointed_by_id",
                table: "competition_experts",
                column: "appointed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_competition_experts_user_id",
                table: "competition_experts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_competition_experts_one_active",
                table: "competition_experts",
                columns: new[] { "competition_id", "user_id" },
                unique: true,
                filter: "is_active");

            migrationBuilder.Sql(
                """
                INSERT INTO competition_experts (competition_id, user_id, appointed_by_id, is_active, created_at, updated_at)
                SELECT DISTINCT a.competition_id, x.reviewer_id, NULL::uuid, true, now(), now()
                  FROM application_assignments x
                  JOIN applications a ON a.id = x.application_id
                 WHERE x.is_active
                UNION
                SELECT DISTINCT d.competition_id, d.reviewer_id, NULL::uuid, true, now(), now()
                  FROM reviewer_declarations d
                 WHERE d.is_active;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_experts");
        }
    }
}
