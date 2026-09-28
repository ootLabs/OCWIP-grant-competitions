using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    format = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    size_in_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment_templates", x => x.id);
                    table.CheckConstraint("ck_attachment_templates_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
                    table.CheckConstraint("ck_attachment_templates_size_positive", "size_in_bytes > 0");
                    table.ForeignKey(
                        name: "fk_attachment_templates_competition_attachments_competition_at",
                        column: x => x.competition_attachment_id,
                        principalTable: "competition_attachments",
                        principalColumn: "id");
                },
                comment: "Files given out with an attachment requirement to fill in (T-102). Public, no personal data.");

            migrationBuilder.CreateIndex(
                name: "ix_attachment_templates_storage_path",
                table: "attachment_templates",
                column: "storage_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attachment_templates_one_active",
                table: "attachment_templates",
                column: "competition_attachment_id",
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_templates");
        }
    }
}
