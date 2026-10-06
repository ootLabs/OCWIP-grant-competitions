using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// A link a mail client cut short, or a probe, is refused the same way as
    /// an unknown one (S-31). FindByIdAsync converts the id straight to a
    /// Guid and throws on anything else, all the way out to a 500, which the
    /// two sibling routes have guarded against since R-35 and this one did
    /// not; a 5xx on an anonymous route is also a false alarm in the
    /// monitoring of T-116.
    /// </summary>
    [RequiresDatabaseTheory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("1")]
    public async Task Confirming_an_email_change_refuses_a_mangled_link_like_an_unknown_one(string userId)
    {
        var host = SessionTestHost.Create(factory, database);
        var client = host.CreateClient();

        var mangled = await client.PostAsJsonAsync(
            "/confirm-email-change", new ConfirmEmailChangeRequest(userId, "cokolwiek"));
        var unknown = await client.PostAsJsonAsync(
            "/confirm-email-change", new ConfirmEmailChangeRequest(Guid.NewGuid().ToString(), "cokolwiek"));

        Assert.Equal(HttpStatusCode.BadRequest, mangled.StatusCode);
        Assert.Equal(unknown.StatusCode, mangled.StatusCode);

        // Read, not just compared: two nulls would match each other and prove
        // nothing, so the text an unknown id gets has to be there first.
        var expected = (await unknown.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail;
        Assert.False(string.IsNullOrEmpty(expected));
        Assert.Equal(expected, (await mangled.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
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

        // The link carries the account and the token, and NOT the address:
        // that one waits on the account, so it stays out of the browser
        // history and out of every proxy log on the way (obserwacja 2).
        Assert.Equal(["token", "userId"], query.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("nowy", link.Query);

        var confirmed = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(query["userId"], query["token"]));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(host, newEmail, SessionTestHost.Password));
        Assert.NotEqual(HttpStatusCode.OK, await LoginStatusAsync(host, oldEmail, SessionTestHost.Password));
        Assert.Single(emails.Sent, x => x.To == oldEmail && x.Subject == "Adres e-mail został zmieniony");

        // The user name moved with the address, in the same write.
        await using (var context = database.CreateContext())
        {
            var stored = await context.Users.AsNoTracking().SingleAsync(x => x.Email == newEmail);
            Assert.Equal((newEmail, stored.NormalizedEmail), (stored.UserName, stored.NormalizedUserName));
        }

        await Task.Delay(1100);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/me")).StatusCode);

        // The link works once.
        var again = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(query["userId"], query["token"]));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_taken_address_is_answered_like_a_free_one_and_gets_a_notice_instead_of_a_link()
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
        // The same number of mails either way, so the time taken says nothing.
        var notice = Assert.Single(emails.Sent, x => x.To == taken);
        Assert.DoesNotMatch(LinkPattern(), notice.Body);
        Assert.Single(emails.Sent, x => x.To == free);
        Assert.Equal(2, emails.Sent.Count(x => x.To == email));
    }

    [RequiresDatabaseFact]
    public async Task A_newer_request_replaces_the_pending_address_and_a_password_change_kills_the_link()
    {
        var (host, emails) = Host();
        var email = SessionTestHost.Email("wiazanie");
        var wanted = SessionTestHost.Email("chciany");
        var later = SessionTestHost.Email("pozniejszy");
        await SessionTestHost.CreateAccountAsync(host, email);
        var session = await LoginAsync(host, email);
        (await session.PostAsJsonAsync("/me/email", new ChangeEmailRequest(wanted, SessionTestHost.Password))).EnsureSuccessStatusCode();
        var link = new Uri(LinkPattern().Match(Assert.Single(emails.Sent, x => x.To == wanted).Body).Value);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);

        // Asked again for another address: the account now waits for that
        // one, so the older link is refused instead of quietly setting an
        // address its owner stopped asking for.
        (await session.PostAsJsonAsync("/me/email", new ChangeEmailRequest(later, SessionTestHost.Password))).EnsureSuccessStatusCode();
        var superseded = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(query["userId"], query["token"]));
        Assert.Equal(HttpStatusCode.BadRequest, superseded.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, await LoginStatusAsync(host, wanted, SessionTestHost.Password));

        // A password changed since: the newer link does not work either.
        var newer = new Uri(LinkPattern().Match(Assert.Single(emails.Sent, x => x.To == later).Body).Value);
        var newerQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(newer.Query);
        (await session.PostAsJsonAsync("/me/password", new ChangePasswordRequest(SessionTestHost.Password, NewPassword))).EnsureSuccessStatusCode();
        var stale = await host.CreateClient().PostAsJsonAsync("/confirm-email-change",
            new ConfirmEmailChangeRequest(newerQuery["userId"], newerQuery["token"]));
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);

        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(host, email, NewPassword));
    }

    [GeneratedRegex(@"http\S+confirm-email-change\S+")]
    private static partial Regex LinkPattern();
}
