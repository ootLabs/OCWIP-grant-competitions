using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Configuration;

/// <summary>
/// T-91: in Production a setting that is only right on a developer's machine
/// stops the start, and the message names the key to fix.
/// </summary>
public sealed class ProductionConfigurationTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["ConnectionStrings:Postgres"] = "Host=db;Database=ocwip;Username=ocwip;Password=x",
        ["EmailVerification:FrontendBaseUrl"] = "https://konkursy.example.pl",
        ["Cors:Origins"] = "https://konkursy.example.pl",
        ["Smtp:Host"] = "smtp.example.pl",
        ["AllowedHosts"] = "konkursy.example.pl;api.konkursy.example.pl",
    };

    private static IConfiguration Build(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static void EnsureValid(IDictionary<string, string?> settings, string environment) =>
        ProductionConfiguration.EnsureValid(Build(settings), new Environment(environment));

    [Fact]
    public void A_complete_configuration_starts()
    {
        EnsureValid(Valid(), Environments.Production);

        Assert.Empty(ProductionConfiguration.Problems(Build(Valid())));
    }

    [Theory]
    [InlineData("ConnectionStrings:Postgres", "", "ConnectionStrings__Postgres")]
    [InlineData("EmailVerification:FrontendBaseUrl", null, "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "http://localhost:3000", "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "https://127.0.0.1", "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "https://[::1]:3000", "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "https://app.localhost", "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "http://konkursy.example.pl", "EmailVerification__FrontendBaseUrl")]
    [InlineData("EmailVerification:FrontendBaseUrl", "konkursy.example.pl", "EmailVerification__FrontendBaseUrl")]
    [InlineData("Cors:Origins", "", "Cors__Origins")]
    [InlineData("Cors:Origins", "http://localhost:3000", "Cors__Origins")]
    [InlineData("Cors:Origins", "https://konkursy.example.pl, http://localhost:3000", "Cors__Origins")]
    // CORS compares the Origin header as text, lowercased and nothing else,
    // and a browser never sends a path or a trailing slash in it.
    [InlineData("Cors:Origins", "https://konkursy.example.pl/", "Cors__Origins")]
    [InlineData("Cors:Origins", "https://konkursy.example.pl/panel", "Cors__Origins")]
    [InlineData("Smtp:Host", "", "Smtp__Host")]
    [InlineData("AllowedHosts", "*", "AllowedHosts")]
    [InlineData("AllowedHosts", "konkursy.example.pl;*", "AllowedHosts")]
    [InlineData("AllowedHosts", "", "AllowedHosts")]
    public void A_development_value_stops_production_naming_its_key(string key, string? value, string named)
    {
        var settings = Valid();
        settings[key] = value;

        var failure = Assert.Throws<InvalidOperationException>(
            () => EnsureValid(settings, Environments.Production));

        Assert.Contains(named, failure.Message);
        Assert.Single(ProductionConfiguration.Problems(Build(settings)));
    }

    [Fact]
    public void Every_problem_is_named_in_one_message()
    {
        // The defaults from appsettings.json: one restart to learn all five,
        // not five restarts to learn one each.
        var failure = Assert.Throws<InvalidOperationException>(() => EnsureValid(
            new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "*",
                ["Cors:Origins"] = "http://localhost:3000",
                ["EmailVerification:FrontendBaseUrl"] = "http://localhost:3000",
            },
            Environments.Production));

        foreach (var key in new[]
        {
            "ConnectionStrings__Postgres", "EmailVerification__FrontendBaseUrl",
            "Cors__Origins", "Smtp__Host", "AllowedHosts",
        })
        {
            Assert.Contains(key, failure.Message);
        }
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void Outside_production_nothing_is_checked(string environment)
    {
        EnsureValid(new Dictionary<string, string?>(), environment);
    }

    [Fact]
    public void The_real_host_refuses_to_start_in_production_with_the_defaults()
    {
        // No database is needed: the check runs before anything touches one,
        // and the empty connection string from appsettings.json is one of the
        // things it reports.
        using var host = new OcwipWebApplicationFactory()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));

        var failure = Assert.ThrowsAny<Exception>(() => host.Services);

        Assert.Contains("EmailVerification__FrontendBaseUrl", failure.ToString());
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Ocwip.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
