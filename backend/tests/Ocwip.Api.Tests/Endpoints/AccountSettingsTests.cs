using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// "Moje konto" (T-106): the password changes only with the old one and
/// ends the other sessions; the address changes only after the new one
/// confirms it, the old one is told, and a taken address is answered like a
/// free one.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed partial class AccountSettingsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private const string NewPassword = "Inn3!Mocn3Haslo";

    private (WebApplicationFactory<Program> Host, RecordingEmailSender Emails) Host()
    {
        var emails = new RecordingEmailSender();
        var host = SessionTestHost.Create(factory, database,
            settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "200" },
            services: s => s.AddSingleton<IEmailSender>(emails));
        return (host, emails);
    }

    private static async Task<HttpStatusCode> LoginStatusAsync(WebApplicationFactory<Program> host, string email, string password) =>
        (await host.CreateClient().PostAsJsonAsync("/login", new { email, password })).StatusCode;

    [RequiresDatabaseFact]
    public async Task A_new_password_needs_the_old_one_and_ends_the_other_sessions()
    {
        var (host, emails) = Host();
        var email = SessionTestHost.Email("haslo");
        await SessionTestHost.CreateAccountAsync(host, email);
        var here = await LoginAsync(host, email);
        var elsewhere = await LoginAsync(host, email);

        var wrong = await here.PostAsJsonAsync("/me/password", new ChangePasswordRequest("zle-haslo", NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("currentPassword", await wrong.Content.ReadAsStringAsync());

        var changed = await here.PostAsJsonAsync("/me/password", new ChangePasswordRequest(SessionTestHost.Password, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // The stamp validator looks once time has moved past the cookie's issue.
        await Task.Delay(1100);
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await elsewhere.GetAsync("/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(host, email, NewPassword));
        Assert.NotEqual(HttpStatusCode.OK, await LoginStatusAsync(host, email, SessionTestHost.Password));
        Assert.Single(emails.Sent, x => x.To == email && x.Subject == "Hasło zostało zmienione");
    }

    [RequiresDatabaseFact]
    public async Task A_session_cannot_guess_the_password_past_the_lockout()
    {
        var (host, _) = Host();
        var email = SessionTestHost.Email("zgadywanie");
        await SessionTestHost.CreateAccountAsync(host, email);
        var session = await LoginAsync(host, email);

        for (var attempt = 0; attempt < Ocwip.Api.Configuration.IdentityConfiguration.DefaultMaxFailedLoginAttempts; attempt++)
        {
            await session.PostAsJsonAsync("/me/password", new ChangePasswordRequest("zle-haslo", NewPassword));
        }

        // Locked out now: even the right password is refused, and nothing changes.
        var right = await session.PostAsJsonAsync("/me/password", new ChangePasswordRequest(SessionTestHost.Password, NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
        Assert.Contains("Za dużo błędnych prób", await right.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_new_address_takes_effect_only_after_its_confirmation_and_the_old_one_is_told()
    {
        var (host, emails) = Host();
        var oldEmail = SessionTestHost.Email("stary");
        var newEmail = SessionTestHost.Email("nowy");
        await SessionTestHost.CreateAccountAsync(host, oldEmail);
        var session = await LoginAsync(host, oldEmail);

        Assert.Equal(HttpStatusCode.NoContent,
            (await session.PostAsJsonAsync("/me/email", new ChangeEmailRequest(newEmail, SessionTestHost.Password))).StatusCode);

        Assert.Single(emails.Sent, x => x.To == oldEmail && x.Subject == "Prośba o zmianę adresu e-mail");
        var confirmation = Assert.Single(emails.Sent, x => x.To == newEmail);

        // Nothing changed yet: the account still signs in with the old address.
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(host, oldEmail, SessionTestHost.Password));
        Assert.NotEqual(HttpStatusCode.OK, await LoginStatusAsync(host, newEmail, SessionTestHost.Password));

        var link = new Uri(LinkPattern().Match(confirmation.Body).Value);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        var confirmed = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(query["userId"], query["email"], query["token"]));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(host, newEmail, SessionTestHost.Password));
        Assert.NotEqual(HttpStatusCode.OK, await LoginStatusAsync(host, oldEmail, SessionTestHost.Password));
        Assert.Single(emails.Sent, x => x.To == oldEmail && x.Subject == "Adres e-mail został zmieniony");

        await Task.Delay(1100);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/me")).StatusCode);

        // The link works once.
        var again = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(query["userId"], query["email"], query["token"]));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_taken_address_is_answered_like_a_free_one_and_gets_no_link()
    {
        var (host, emails) = Host();
        var email = SessionTestHost.Email("zmienia");
        var taken = SessionTestHost.Email("zajety");
        var free = SessionTestHost.Email("wolny");
        await SessionTestHost.CreateAccountAsync(host, email);
        await SessionTestHost.CreateAccountAsync(host, taken);
        var session = await LoginAsync(host, email);

        var toTaken = await session.PostAsJsonAsync("/me/email", new ChangeEmailRequest(taken, SessionTestHost.Password));
        var toFree = await session.PostAsJsonAsync("/me/email", new ChangeEmailRequest(free, SessionTestHost.Password));

        Assert.Equal(toFree.StatusCode, toTaken.StatusCode);
        Assert.Equal(await toFree.Content.ReadAsStringAsync(), await toTaken.Content.ReadAsStringAsync());
        Assert.DoesNotContain(emails.Sent, x => x.To == taken);
        Assert.Single(emails.Sent, x => x.To == free);
    }

    [GeneratedRegex(@"http\S+confirm-email-change\S+")]
    private static partial Regex LinkPattern();
}
