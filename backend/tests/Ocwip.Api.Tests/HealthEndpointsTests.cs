using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Database_probe_answers_503_for_a_connection_string_it_cannot_even_parse()
    {
        // T-111: not an NpgsqlException, so the probe used to answer 500.
        // Only the probe gets the broken string; the rest of the host keeps its own.
        var broken = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=nie-liczba" })
            .Build();
        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddSingleton(new Ocwip.Api.Data.DatabaseProbe(broken))))
            .CreateClient();

        var response = await client.GetAsync("/health/db");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("nie-liczba", await response.Content.ReadAsStringAsync());
    }
}
