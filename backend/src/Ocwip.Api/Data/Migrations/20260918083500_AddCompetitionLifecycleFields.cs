using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// T-20. The competition grows the columns its lifecycle needs: a number
    /// the organisation refers to it by, the moment it was published, the
    /// continuous intake switch (which is why end_date becomes nullable) and
    /// the form version in force.
    /// </summary>
    public partial class AddCompetitionLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "end_date",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Competition closing date and time stored in UTC, truncated to a whole minute. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Competition closing date and time stored in UTC, truncated to a whole minute. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes.");

            migrationBuilder.AddColumn<Guid>(
                name: "form_definition_id",
                table: "competitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_continuous_intake",
                table: "competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                comment: "True when the intake never closes on its own, in which case end_date is null.");

            // Three steps rather than one AddColumn with a default, and both
            // halves of that matter. A default of '' under a unique index
            // fails the moment a second row exists, and a default left on the
            // column afterwards means a later insert can silently skip a
            // number that has to come from the organisation.
            migrationBuilder.AddColumn<string>(
                name: "number",
                table: "competitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Rows predating this migration have no number anybody can look
            // up, and the identifier is the only value already unique per row.
            // An operator renames it; nothing reads it before then.
            migrationBuilder.Sql(
                "UPDATE competitions SET number = id::text WHERE number IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "number",
                table: "competitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When an operator published the competition, in UTC. Null while it is still a draft.");

            migrationBuilder.CreateIndex(
                name: "ix_competitions_id_form_definition_id",
                table: "competitions",
                columns: new[] { "id", "form_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_competitions_number",
                table: "competitions",
                column: "number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_competitions_end_date_matches_continuous_intake",
                table: "competitions",
                sql: "(end_date IS NULL) = is_continuous_intake");

            migrationBuilder.AddForeignKey(
                name: "fk_competitions_form_definitions_id_form_definition_id",
                table: "competitions",
                columns: new[] { "id", "form_definition_id" },
                principalTable: "form_definitions",
                principalColumns: new[] { "competition_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // end_date goes back to NOT NULL here, and a continuous intake has
            // no closing date to put there. Inventing one would turn a
            // competition that never closes into one that closes on a date
            // nobody chose, which is worse than refusing. So the migration
            // reverts cleanly while no continuous competition exists, and
            // stops with an explanation once one does.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM competitions WHERE end_date IS NULL) THEN
                        RAISE EXCEPTION
                            'Cannot revert AddCompetitionLifecycleFields: % competition(s) have a continuous intake and therefore no closing date to restore. Give them a closing date first.',
                            (SELECT count(*) FROM competitions WHERE end_date IS NULL);
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_competitions_form_definitions_id_form_definition_id",
                table: "competitions");

            migrationBuilder.DropIndex(
                name: "ix_competitions_id_form_definition_id",
                table: "competitions");

            migrationBuilder.DropIndex(
                name: "ix_competitions_number",
                table: "competitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_competitions_end_date_matches_continuous_intake",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "form_definition_id",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "is_continuous_intake",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "number",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "competitions");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "end_date",
                table: "competitions",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Competition closing date and time stored in UTC, truncated to a whole minute. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Competition closing date and time stored in UTC, truncated to a whole minute. Submission is rejected at or after this moment. UTC is used to avoid ambiguity caused by local time zones and daylight saving time changes.");
        }
    }
}
