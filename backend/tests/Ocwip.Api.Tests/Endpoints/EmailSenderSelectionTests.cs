using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>T-43a: a configured relay is used; a relay without a sender stops the start instead of failing each mail.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailSenderSelectionTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public EmailSenderSelectionTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public void A_configured_relay_sends_and_no_relay_logs()
    {
        using var relay = SessionTestHost.Create(_factory, _database, new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.org",
            ["Smtp:From"] = "konkursy@ocwip.example",
        });
        using var none = SessionTestHost.Create(_factory, _database);

        Assert.IsType<SmtpEmailSender>(Resolve(relay));
        Assert.IsType<EmailSenderService>(Resolve(none));
    }

    [RequiresDatabaseFact]
    public void A_relay_without_a_sender_address_does_not_start()
    {
        using var host = SessionTestHost.Create(_factory, _database, new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.org",
        });

        var failure = Assert.ThrowsAny<Exception>(() => host.Services);
        Assert.Contains("SMTP__FROM", failure.ToString());
    }

    [RequiresDatabaseFact]
    public void Port_465_is_refused_at_start_rather_than_hanging_on_every_mail()
    {
        using var host = SessionTestHost.Create(_factory, _database, new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.org",
            ["Smtp:From"] = "konkursy@ocwip.example",
            ["Smtp:Port"] = "465",
        });

        var failure = Assert.ThrowsAny<Exception>(() => host.Services);
        Assert.Contains("587", failure.ToString());
    }

    private static IEmailSender Resolve(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEmailSender>();
    }
}
