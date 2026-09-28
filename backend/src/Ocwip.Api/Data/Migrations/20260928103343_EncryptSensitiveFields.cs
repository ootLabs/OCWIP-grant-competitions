using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// T-47a: the encrypted columns become text without a length, since a
    /// ciphertext is longer than its plaintext, and their comments say what
    /// is encrypted. The data itself is encrypted by the application, not
    /// here: a migration has no key. Rows written before this stay readable
    /// as plaintext until the reencrypt-data command rewrites them.
    ///
    /// Down only works on a database without encrypted values: a ciphertext
    /// does not fit the old widths and is not JSON, and failing is better
    /// than cutting it.
    /// </summary>
    public partial class EncryptSensitiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "pesel",
                table: "users",
                type: "text",
                nullable: true,
                comment: "PESEL. Sensitive personal data, encrypted (T-47a). Null until the agreement stage.",
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11,
                oldNullable: true,
                oldComment: "PESEL. Sensitive personal data, encrypted at rest in T-80. Null until the agreement stage.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "prefill",
                table: "reports",
                type: "jsonb",
                nullable: false,
                comment: "Values taken from the application when the report was started, restored on every save. Sensitive ones encrypted like answers (T-47a).",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Values taken from the application when the report was started, restored on every save.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "reports",
                type: "jsonb",
                nullable: false,
                comment: "The applicant's answers. Answers of sensitive fields, and of fields prefilled from one, encrypted inside the document (T-47a).",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "The applicant's answers. Holds personal data (contact person, group leader); sensitive.");

            migrationBuilder.AlterColumn<string>(
                name: "representatives",
                table: "entities",
                type: "text",
                nullable: false,
                defaultValueSql: "'[]'",
                comment: "People authorised to represent the organisation: first name, last name, function, as encrypted JSON. Sensitive personal data (T-47a).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb",
                oldComment: "People authorised to represent the organisation: first name, last name, function. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "entities",
                type: "text",
                nullable: true,
                comment: "Phone. Sensitive personal data, encrypted (T-47a).",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true,
                oldComment: "Phone. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<string>(
                name: "nip",
                table: "entities",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                comment: "NIP, 10 digits. Required for an organisation only, checked at the API edge and not by the schema. Plaintext by decision DZ-2: only organisations carry one, and an organisation's NIP is public.",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true,
                oldComment: "NIP, 10 digits. Required for an organisation only, checked at the API edge and not by the schema. Sensitive data, encrypted at rest in T-80. 10 fits the plaintext number and no ciphertext at all, so T-80 owns widening this column; without that the first encrypted write fails on 22001.");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "entities",
                type: "text",
                nullable: true,
                comment: "E-mail of the entity, formerly contact_information. Sensitive personal data, encrypted (T-47a).",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true,
                oldComment: "E-mail of the entity, formerly contact_information. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<string>(
                name: "correspondence_address",
                table: "entities",
                type: "text",
                nullable: true,
                comment: "Correspondence address when it differs from the registered one. Sensitive personal data, encrypted (T-47a).",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Correspondence address when it differs from the registered one. Sensitive personal data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<string>(
                name: "bank_account",
                table: "entities",
                type: "text",
                nullable: true,
                comment: "Bank account (NRB), 26 digits without spaces. Sensitive data, encrypted (T-47a).",
                oldClrType: typeof(string),
                oldType: "character varying(26)",
                oldMaxLength: 26,
                oldNullable: true,
                oldComment: "Bank account (NRB), 26 digits without spaces. Sensitive data, in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "entities",
                type: "text",
                nullable: true,
                comment: "Address. Required for an organisation only, checked at the API edge. Sensitive personal data, encrypted (T-47a).",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Address. Required for an organisation only, checked at the API edge. Sensitive personal data, encrypted at rest in T-80, which owns checking that 500 still holds the ciphertext.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "values",
                table: "contracts",
                type: "jsonb",
                nullable: false,
                comment: "Placeholder values typed by the operator, each encrypted inside the object (T-47a). Holds personal data (people who sign, PESEL, bank account).",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Placeholder values typed by the operator. Holds personal data (people who sign, bank account); sensitive.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "entity_snapshot",
                table: "applications",
                type: "jsonb",
                nullable: true,
                comment: "The entity card as it stood when the application was submitted (T-93). Null on a draft. Addresses, phone, e-mail, bank account and representatives are encrypted inside the document (T-47a).",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "The entity card as it stood when the application was submitted (T-93). Null on a draft. Holds personal data (representatives, contact details), in scope for encryption at rest in T-47a.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "applications",
                type: "jsonb",
                nullable: false,
                comment: "Answers stored as JSONB, shaped by the form definition this application points at. The contract of this column is settled together with the definition contract in card T-20. The answers of fields the form marks sensitive are encrypted INSIDE the document (T-47a): ciphertext is neither an object nor an array, so encrypting the whole column would mean dropping both the jsonb type and the check constraint below, and with them the searchability jsonb was chosen for.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Answers stored as JSONB, shaped by the form definition this application points at. The contract of this column is settled together with the definition contract in card T-20. Holds personal data, so T-80 has to encrypt the sensitive fields INSIDE the document: ciphertext is neither an object nor an array, so encrypting the whole column would mean dropping both the jsonb type and the check constraint below, and with them the searchability jsonb was chosen for.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "pesel",
                table: "users",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true,
                comment: "PESEL. Sensitive personal data, encrypted at rest in T-80. Null until the agreement stage.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "PESEL. Sensitive personal data, encrypted (T-47a). Null until the agreement stage.");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "prefill",
                table: "reports",
                type: "jsonb",
                nullable: false,
                comment: "Values taken from the application when the report was started, restored on every save.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Values taken from the application when the report was started, restored on every save. Sensitive ones encrypted like answers (T-47a).");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "reports",
                type: "jsonb",
                nullable: false,
                comment: "The applicant's answers. Holds personal data (contact person, group leader); sensitive.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "The applicant's answers. Answers of sensitive fields, and of fields prefilled from one, encrypted inside the document (T-47a).");

            // No automatic cast from text back to jsonb: USING, by hand.
            migrationBuilder.Sql("""
                ALTER TABLE entities ALTER COLUMN representatives DROP DEFAULT;
                ALTER TABLE entities ALTER COLUMN representatives TYPE jsonb USING representatives::jsonb;
                ALTER TABLE entities ALTER COLUMN representatives SET DEFAULT '[]'::jsonb;
                COMMENT ON COLUMN entities.representatives IS 'People authorised to represent the organisation: first name, last name, function. Sensitive personal data, in scope for encryption at rest in T-47a.';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "entities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                comment: "Phone. Sensitive personal data, in scope for encryption at rest in T-47a.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Phone. Sensitive personal data, encrypted (T-47a).");

            migrationBuilder.AlterColumn<string>(
                name: "nip",
                table: "entities",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                comment: "NIP, 10 digits. Required for an organisation only, checked at the API edge and not by the schema. Sensitive data, encrypted at rest in T-80. 10 fits the plaintext number and no ciphertext at all, so T-80 owns widening this column; without that the first encrypted write fails on 22001.",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true,
                oldComment: "NIP, 10 digits. Required for an organisation only, checked at the API edge and not by the schema. Plaintext by decision DZ-2: only organisations carry one, and an organisation's NIP is public.");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "entities",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                comment: "E-mail of the entity, formerly contact_information. Sensitive personal data, in scope for encryption at rest in T-47a.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "E-mail of the entity, formerly contact_information. Sensitive personal data, encrypted (T-47a).");

            migrationBuilder.AlterColumn<string>(
                name: "correspondence_address",
                table: "entities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Correspondence address when it differs from the registered one. Sensitive personal data, in scope for encryption at rest in T-47a.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Correspondence address when it differs from the registered one. Sensitive personal data, encrypted (T-47a).");

            migrationBuilder.AlterColumn<string>(
                name: "bank_account",
                table: "entities",
                type: "character varying(26)",
                maxLength: 26,
                nullable: true,
                comment: "Bank account (NRB), 26 digits without spaces. Sensitive data, in scope for encryption at rest in T-47a.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Bank account (NRB), 26 digits without spaces. Sensitive data, encrypted (T-47a).");

            migrationBuilder.AlterColumn<string>(
                name: "address",
                table: "entities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Address. Required for an organisation only, checked at the API edge. Sensitive personal data, encrypted at rest in T-80, which owns checking that 500 still holds the ciphertext.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Address. Required for an organisation only, checked at the API edge. Sensitive personal data, encrypted (T-47a).");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "values",
                table: "contracts",
                type: "jsonb",
                nullable: false,
                comment: "Placeholder values typed by the operator. Holds personal data (people who sign, bank account); sensitive.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Placeholder values typed by the operator, each encrypted inside the object (T-47a). Holds personal data (people who sign, PESEL, bank account).");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "entity_snapshot",
                table: "applications",
                type: "jsonb",
                nullable: true,
                comment: "The entity card as it stood when the application was submitted (T-93). Null on a draft. Holds personal data (representatives, contact details), in scope for encryption at rest in T-47a.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "The entity card as it stood when the application was submitted (T-93). Null on a draft. Addresses, phone, e-mail, bank account and representatives are encrypted inside the document (T-47a).");

            migrationBuilder.AlterColumn<JsonElement>(
                name: "answers",
                table: "applications",
                type: "jsonb",
                nullable: false,
                comment: "Answers stored as JSONB, shaped by the form definition this application points at. The contract of this column is settled together with the definition contract in card T-20. Holds personal data, so T-80 has to encrypt the sensitive fields INSIDE the document: ciphertext is neither an object nor an array, so encrypting the whole column would mean dropping both the jsonb type and the check constraint below, and with them the searchability jsonb was chosen for.",
                oldClrType: typeof(JsonElement),
                oldType: "jsonb",
                oldComment: "Answers stored as JSONB, shaped by the form definition this application points at. The contract of this column is settled together with the definition contract in card T-20. The answers of fields the form marks sensitive are encrypted INSIDE the document (T-47a): ciphertext is neither an object nor an array, so encrypting the whole column would mean dropping both the jsonb type and the check constraint below, and with them the searchability jsonb was chosen for.");
        }
    }
}
