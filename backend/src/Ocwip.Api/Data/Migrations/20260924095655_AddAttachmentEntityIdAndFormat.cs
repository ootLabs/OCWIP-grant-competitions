using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentEntityIdAndFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "entity_id",
                table: "attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                comment: "Copied from the owning application's entity_id at upload time (T-32), so the authorization handler can answer \"whose is this\" from the attachment row alone.");

            migrationBuilder.AddColumn<string>(
                name: "format",
                table: "attachments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_entity_id",
                table: "attachments",
                column: "entity_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_entities_entity_id",
                table: "attachments",
                column: "entity_id",
                principalTable: "entities",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_entities_entity_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_entity_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "entity_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "format",
                table: "attachments");
        }
    }
}
