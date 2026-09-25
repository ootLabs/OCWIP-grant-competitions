using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, comment: "False marks the assignment revoked. Rows are never removed, because retention is at least 5 years and this row is the proof a reviewer once had access."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "When the assignment was revoked, in UTC. Null while active.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_assignments", x => x.id);
                    table.CheckConstraint("ck_application_assignments_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_application_assignments_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_application_assignments_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_assignments_application_id_reviewer_id",
                table: "application_assignments",
                columns: new[] { "application_id", "reviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_assignments_reviewer_id_is_active",
                table: "application_assignments",
                columns: new[] { "reviewer_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_assignments");
        }
    }
}
