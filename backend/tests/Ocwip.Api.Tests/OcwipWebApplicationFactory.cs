using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Services;

namespace Ocwip.Api.Tests;

/// <summary>
/// Boots the real application with startup migrations and background jobs turned off.
///
/// Every test that starts the host goes through this factory. A plain
/// WebApplicationFactory would inherit Database:MigrateOnStartup from the
/// container or from CI and run DDL against the shared ocwip database, which
/// is the one being worked on. Migrations are covered by MigrationTests, on a
/// database created for that single test.
/// </summary>
public class OcwipWebApplicationFactory : WebApplicationFactory<Program>
{
    // Background jobs off too (T-105): a job running on its own timer behind
    // a test would send mail the test never asked for. Tests call a job directly.
    //
    // The Turnstile key too: the dev container carries Cloudflare's test key,
    // and a test must not ask Cloudflare anything. HumanCheckTests turns it
    // on with a stand-in for Cloudflare.
    //
    // Account mail too: a test reads the mail a request sent right after the
    // answer, so it is delivered inline instead of by the queue's own thread.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("BackgroundJobs:Enabled", "false")
            .UseSetting("Turnstile:SecretKey", "")
            .ConfigureServices(services => services.AddScoped<IAccountMailQueue, InlineAccountMailQueue>());
}
