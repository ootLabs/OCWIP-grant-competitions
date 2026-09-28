using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_attachments_application_id",
                table: "attachments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deactivated_at",
                table: "competition_cost_categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "competition_cost_categories",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "False once an edit took the row off the list. Rows are never removed (retention).");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deactivated_at",
                table: "competition_contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "competition_contacts",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "False once an edit took the row off the list. Rows are never removed (retention).");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deactivated_at",
                table: "competition_attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "competition_attachments",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "False once an edit took the row off the list. Rows are never removed (retention).");

            migrationBuilder.AddColumn<Guid>(
                name: "competition_attachment_id",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_competition_cost_categories_deactivated_at_matches_is_active",
                table: "competition_cost_categories",
                sql: "is_active = (deactivated_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competition_contacts_deactivated_at_matches_is_active",
                table: "competition_contacts",
                sql: "is_active = (deactivated_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_competition_attachments_deactivated_at_matches_is_active",
                table: "competition_attachments",
                sql: "is_active = (deactivated_at IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_application_id_competition_attachment_id",
                table: "attachments",
                columns: new[] { "application_id", "competition_attachment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_competition_attachment_id",
                table: "attachments",
                column: "competition_attachment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_competition_attachments_competition_attachment_",
                table: "attachments",
                column: "competition_attachment_id",
                principalTable: "competition_attachments",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_competition_attachments_competition_attachment_",
                table: "attachments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competition_cost_categories_deactivated_at_matches_is_active",
                table: "competition_cost_categories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competition_contacts_deactivated_at_matches_is_active",
                table: "competition_contacts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competition_attachments_deactivated_at_matches_is_active",
                table: "competition_attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_application_id_competition_attachment_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_competition_attachment_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "deactivated_at",
                table: "competition_cost_categories");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "competition_cost_categories");

            migrationBuilder.DropColumn(
                name: "deactivated_at",
                table: "competition_contacts");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "competition_contacts");

            migrationBuilder.DropColumn(
                name: "deactivated_at",
                table: "competition_attachments");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "competition_attachments");

            migrationBuilder.DropColumn(
                name: "competition_attachment_id",
                table: "attachments");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_application_id",
                table: "attachments",
                column: "application_id");
        }
    }
}
