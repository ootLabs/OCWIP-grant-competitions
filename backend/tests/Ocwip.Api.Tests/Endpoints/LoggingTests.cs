using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-116: outside Development the log is JSON with the request scopes, and
/// every answer names the request its log lines carry.
/// </summary>
public sealed class LoggingTests(OcwipWebApplicationFactory factory) : IClassFixture<OcwipWebApplicationFactory>
{
    [Fact]
    public void Outside_development_the_console_writes_json_with_scopes()
    {
        var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Staging"));

        var console = host.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        var json = host.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;

        Assert.Equal(ConsoleFormatterNames.Json, console.FormatterName);
        Assert.True(json.IncludeScopes);
    }

    [Fact]
    public void Development_keeps_the_plain_console()
    {
        var console = factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;

        Assert.NotEqual(ConsoleFormatterNames.Json, console.FormatterName);
    }

    [Fact]
    public async Task Every_answer_names_its_request()
    {
        var client = factory.CreateClient();

        var first = await client.GetAsync("/health");
        var second = await client.GetAsync("/no-such-path");

        var id = Assert.Single(first.Headers.GetValues(LoggingConfiguration.RequestIdHeader));
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotEqual(id, Assert.Single(second.Headers.GetValues(LoggingConfiguration.RequestIdHeader)));
    }
}
