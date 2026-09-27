using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntityCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renamed, not dropped and added: contact_information held the
            // e-mail address already, and dropping it would lose every row's.
            migrationBuilder.RenameColumn(
                name: "contact_information",
                table: "entities",
                newName: "email");

            // contact_information was NOT NULL varchar(500): an empty value is
            // no e-mail at all, and a value over 320 characters would make the
            // narrowing below fail the whole migration. Neither is a valid
            // address, so the card asks for a new one either way.
            // Truncated before the narrowing, emptied to NULL only after it:
            // the column is still NOT NULL here.
            migrationBuilder.Sql("UPDATE entities SET email = left(email, 320) WHERE length(email) > 320;");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "entities",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                comment: "E-mail of the entity, formerly contact_information. Sensitive personal data, in scope for encryption at rest in T-47a.",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.Sql("UPDATE entities SET email = NULL WHERE email = '';");

            migrationBuilder.AddColumn<string>(
                name: "bank_account",
                table: "entities",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true,
                comment: "Bank account (NRB), 26 digits without spaces. Sensitive data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AddColumn<string>(
                name: "correspondence_address",
                table: "entities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Correspondence address when it differs from the registered one. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AddColumn<string>(
                name: "legal_form",
                table: "entities",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                comment: "Legal form of an organisation card (T-93). Null for an informal group.");

            migrationBuilder.AddColumn<string>(
                name: "legal_form_other",
                table: "entities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "The legal form's name when legal_form is Other.");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "entities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Phone. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AddColumn<string>(
                name: "register",
                table: "entities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                comment: "KRS or another register (T-93).");

            migrationBuilder.AddColumn<string>(
                name: "register_number",
                table: "entities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "Ten digits in KRS, free text in another register.");

            migrationBuilder.AddColumn<string>(
                name: "regon",
                table: "entities",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true,
                comment: "REGON, 9 or 14 digits, optional.");

            migrationBuilder.AddColumn<string>(
                name: "representatives",
                table: "entities",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                comment: "People authorised to represent the organisation: first name, last name, function. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AddColumn<JsonElement>(
                name: "entity_snapshot",
                table: "applications",
                type: "jsonb",
                nullable: true,
                comment: "The entity card as it stood when the application was submitted (T-93). Null on a draft. Holds personal data (representatives, contact details), in scope for encryption at rest in T-47a.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_account",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "correspondence_address",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "legal_form",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "legal_form_other",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "register",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "register_number",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "regon",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "representatives",
                table: "entities");

            migrationBuilder.DropColumn(
                name: "entity_snapshot",
                table: "applications");

            migrationBuilder.Sql("UPDATE entities SET email = '' WHERE email IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "entities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                comment: "Contact details of the entity. For an informal group these are a natural person's, so they are sensitive personal data and in scope for encryption at rest in T-80, which owns checking that 500 still holds the ciphertext.",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "email",
                table: "entities",
                newName: "contact_information");
        }
    }
}
