using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocwip.Api.Data.Migrations
{
    /// <summary>
    /// T-113: migrations run as a role that may change the schema, the
    /// application as ocwip_app, which may only read and write rows. Granted
    /// here, by the role that owns the tables, and only when ocwip_app exists:
    /// the development stack has one role for both, and there this does
    /// nothing. The default privileges cover every table and sequence a later
    /// migration creates, as long as that migration runs as the same role.
    /// </summary>
    public partial class AppRoleGrants : Migration
    {
        internal const string Grant = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ocwip_app') THEN
                    GRANT USAGE ON SCHEMA public TO ocwip_app;
                    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ocwip_app;
                    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ocwip_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ocwip_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        GRANT USAGE, SELECT ON SEQUENCES TO ocwip_app;
                END IF;
            END
            $$;
            """;

        internal const string Revoke = """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ocwip_app') THEN
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM ocwip_app;
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        REVOKE USAGE, SELECT ON SEQUENCES FROM ocwip_app;
                    REVOKE SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public FROM ocwip_app;
                    REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public FROM ocwip_app;
                END IF;
            END
            $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Grant);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Revoke);
        }
    }
}
