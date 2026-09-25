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
            // Both columns start nullable and are filled from what every
            // existing row already knows. Added straight as NOT NULL with a
            // default, the all zero entity_id broke the foreign key below on
            // the first row that was already there, and an empty format
            // cannot be read back as AllowedFileFormat.
            migrationBuilder.AddColumn<Guid>(
                name: "entity_id",
                table: "attachments",
                type: "uuid",
                nullable: true,
                comment: "Copied from the owning application's entity_id at upload time (T-32), so the authorization handler can answer \"whose is this\" from the attachment row alone.");

            migrationBuilder.AddColumn<string>(
                name: "format",
                table: "attachments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE attachments AS a
                   SET entity_id = ap.entity_id
                  FROM applications AS ap
                 WHERE ap.id = a.application_id;
                """);

            // Rows from before T-32 carry only the declared content type, so
            // the format comes from that, and from the file name's extension
            // when the type is not one of ours. The names match
            // AllowedFileFormat, which is stored as text.
            migrationBuilder.Sql(
                """
                UPDATE attachments
                   SET format = CASE
                       WHEN content_type = 'application/pdf' THEN 'Pdf'
                       WHEN content_type = 'application/msword' THEN 'Doc'
                       WHEN content_type = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' THEN 'Docx'
                       WHEN content_type = 'application/vnd.ms-excel' THEN 'Xls'
                       WHEN content_type = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' THEN 'Xlsx'
                       WHEN content_type = 'image/jpeg' THEN 'Jpg'
                       WHEN content_type = 'application/vnd.oasis.opendocument.text' THEN 'Odt'
                       WHEN content_type = 'application/vnd.oasis.opendocument.spreadsheet' THEN 'Ods'
                       WHEN lower(file_name) LIKE '%.pdf' THEN 'Pdf'
                       WHEN lower(file_name) LIKE '%.doc' THEN 'Doc'
                       WHEN lower(file_name) LIKE '%.docx' THEN 'Docx'
                       WHEN lower(file_name) LIKE '%.xls' THEN 'Xls'
                       WHEN lower(file_name) LIKE '%.xlsx' THEN 'Xlsx'
                       WHEN lower(file_name) LIKE '%.jpg' OR lower(file_name) LIKE '%.jpeg' THEN 'Jpg'
                       WHEN lower(file_name) LIKE '%.odt' THEN 'Odt'
                       WHEN lower(file_name) LIKE '%.ods' THEN 'Ods'
                   END;
                """);

            // Refuse rather than guess: a row whose format neither the type
            // nor the name gives away needs a person to look at it, and the
            // whole migration is one transaction, so nothing stays half done.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM attachments WHERE entity_id IS NULL OR format IS NULL) THEN
                        RAISE EXCEPTION 'attachments: % row(s) whose owner or format cannot be derived; fix them before migrating',
                            (SELECT count(*) FROM attachments WHERE entity_id IS NULL OR format IS NULL);
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "entity_id",
                table: "attachments",
                type: "uuid",
                nullable: false,
                comment: "Copied from the owning application's entity_id at upload time (T-32), so the authorization handler can answer \"whose is this\" from the attachment row alone.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Copied from the owning application's entity_id at upload time (T-32), so the authorization handler can answer \"whose is this\" from the attachment row alone.");

            migrationBuilder.AlterColumn<string>(
                name: "format",
                table: "attachments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

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
