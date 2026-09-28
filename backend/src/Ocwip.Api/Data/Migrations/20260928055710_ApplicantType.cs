using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicantType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "applicant_type",
                table: "applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                comment: "Kind of applicant as submitted (T-94): the answer of the applicantType field, or the entity's type when the form has none. Null on a draft.");

            // Every application submitted before T-94 was evaluated by its
            // entity's type, so that is the kind it was submitted as. Filled
            // in before the constraint, which would refuse the rows otherwise.
            migrationBuilder.Sql(
                "UPDATE applications a SET applicant_type = e.type " +
                "FROM entities e WHERE e.id = a.entity_id AND a.status <> 'Draft';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_applicant_type_matches_status",
                table: "applications",
                sql: "(status <> 'Draft') = (applicant_type IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_applicant_type_matches_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "applicant_type",
                table: "applications");
        }
    }
}
