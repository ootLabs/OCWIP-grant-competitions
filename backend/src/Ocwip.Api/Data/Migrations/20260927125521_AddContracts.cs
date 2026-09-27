using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_templates", x => x.id);
                    table.CheckConstraint("ck_document_templates_version_positive", "version_number > 0");
                    table.ForeignKey(
                        name: "fk_document_templates_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    values = table.Column<JsonElement>(type: "jsonb", nullable: false, comment: "Placeholder values typed by the operator. Holds personal data (people who sign, bank account); sensitive."),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    signed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.id);
                    table.CheckConstraint("ck_contracts_signed_on_matches_status", "(status = 'Signed') = (signed_on IS NOT NULL)");
                    table.CheckConstraint("ck_contracts_values_is_an_object", "jsonb_typeof(values) = 'object'");
                    table.ForeignKey(
                        name: "fk_contracts_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_contracts_competitions_competition_id",
                        column: x => x.competition_id,
                        principalTable: "competitions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_contracts_document_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "document_templates",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_contracts_entities_entity_id",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_contracts_competition_id",
                table: "contracts",
                column: "competition_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_entity_id",
                table: "contracts",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_template_id",
                table: "contracts",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ux_contracts_one_active_per_application",
                table: "contracts",
                column: "application_id",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_document_templates_competition_id_kind_version_number",
                table: "document_templates",
                columns: new[] { "competition_id", "kind", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "document_templates");
        }
    }
}
