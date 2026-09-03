using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Tests.Data;

namespace Ocwip.Api.Tests;

/// <summary>
/// Boots the real application with startup migrations turned off.
///
/// Every test that starts the host goes through this factory. A plain
/// WebApplicationFactory would inherit Database:MigrateOnStartup from the
/// container or from CI and run DDL against the shared ocwip database, which
/// is the one being worked on. Migrations are covered by MigrationTests, on a
/// database created for that single test.
/// </summary>
public class OcwipWebApplicationFactory : WebApplicationFactory<Program>
{
   

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("Database:MigrateOnStartup", "false");
}

