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
    // Account mail too: a test reads the mail a request sent right after the
    // answer, so it is delivered inline instead of by the queue's own thread.
    //
    // No relay either, and the whole Smtp section back to the values
    // SmtpOptions itself defaults to. The development container points the
    // backend at Mailpit, and "dotnet test" inside that container inherits
    // every Smtp__ variable, so a run would otherwise talk to the mailbox
    // instead of collecting mail in memory. The sender address matters as
    // much as the host: a test that sets only Smtp:Host, to prove the API
    // refuses to start without a sender, passes its own check while the
    // container quietly supplies Smtp__From.
    //
    // A test that wants a relay sets these itself through SessionTestHost,
    // whose settings are applied after these and win.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("BackgroundJobs:Enabled", "false")
            .UseSetting("Smtp:Host", string.Empty)
            .UseSetting("Smtp:From", string.Empty)
            .UseSetting("Smtp:Port", "587")
            .UseSetting("Smtp:EnableSsl", "true")
            .ConfigureServices(services => services.AddScoped<IAccountMailQueue, InlineAccountMailQueue>());
}
