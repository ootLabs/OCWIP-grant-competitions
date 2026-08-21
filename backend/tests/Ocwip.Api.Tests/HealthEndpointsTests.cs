using System.Net;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// Boots the real application in memory.
///
/// The database probe is tested with the connection string explicitly cleared,
/// not by relying on there being no database around: the same test has to give
/// the same answer on a laptop, in a container that can reach Postgres, and in CI.
/// </summary>
public class HealthEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;

    public HealthEndpointsTests(OcwipWebApplicationFactory factory) => _factory = factory;

    private HttpClient ClientWithoutDatabase() =>
        _factory
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Postgres", string.Empty))
            .CreateClient();

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_returns_ok_when_the_database_is_unreachable()
    {
        // Port 1 is closed, so this is a database that is configured and down.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting(
                "ConnectionStrings:Postgres",
                "Host=127.0.0.1;Port=1;Database=ocwip;Username=ocwip;Password=ocwip"))
            .CreateClient();

        var response = await client.GetAsync("/health");

        // Liveness must not depend on the database, and the host must not die
        // trying to migrate one it cannot reach while starting.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Database_probe_reports_unavailable_without_a_connection_string()
    {
        var response = await ClientWithoutDatabase().GetAsync("/health/db");

        // Not 500: an unreachable database is a known state, not an unhandled crash.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Database_probe_never_leaks_connection_details()
    {
        var response = await ClientWithoutDatabase().GetAsync("/health/db");

        var body = await response.Content.ReadAsStringAsync();

        // Credentials in an error body reach browser consoles and log aggregators.
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("host", body, StringComparison.OrdinalIgnoreCase);
    }
}
