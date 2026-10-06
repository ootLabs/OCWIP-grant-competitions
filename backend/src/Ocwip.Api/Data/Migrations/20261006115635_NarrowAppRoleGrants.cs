using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// S-09: the application's role gets what the application does, and
    /// nothing else. AppRoleGrants gave ocwip_app SELECT, INSERT, UPDATE and
    /// DELETE on every table and, through ALTER DEFAULT PRIVILEGES, on every
    /// table a later migration would create, so the rule "nie kasujemy
    /// twardo" (AGENTS.md 5) and five years of retention stood on the
    /// discipline of the code alone: nothing in backend/src deletes a row,
    /// and nothing in the database said it must not.
    ///
    /// Two narrowings. DELETE goes everywhere, because no path in the product
    /// deletes a row; a future one that needs it says so in a migration of
    /// its own. UPDATE goes from the tables that only ever grow, which are
    /// the record of what happened: who read personal data, how an
    /// application and a report moved between states, which versions were
    /// submitted, and which consents were accepted. An attacker holding the
    /// application's connection can still write the product's data; what they
    /// can no longer do is erase the trail.
    /// </summary>
    public partial class NarrowAppRoleGrants : Migration
    {
        /// <summary>Tables the application only ever adds rows to.</summary>
        internal const string AppendOnly =
            "personal_data_reads, application_status_history, report_status_history, "
            + "application_versions, report_versions, consent_acceptances";

        internal const string Narrow = $"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ocwip_app') THEN
                    REVOKE DELETE ON ALL TABLES IN SCHEMA public FROM ocwip_app;
                    REVOKE UPDATE ON {AppendOnly} FROM ocwip_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        REVOKE DELETE ON TABLES FROM ocwip_app;
                END IF;
            END
            $$;
            """;

        internal const string Widen = $"""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ocwip_app') THEN
                    GRANT DELETE ON ALL TABLES IN SCHEMA public TO ocwip_app;
                    GRANT UPDATE ON {AppendOnly} TO ocwip_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        GRANT DELETE ON TABLES TO ocwip_app;
                END IF;
            END
            $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Narrow);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Widen);
        }
    }
}
