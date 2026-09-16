using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// POST /forgot-password and POST /reset-password over real HTTP against a
/// real PostgreSQL. IEmailSender is swapped for RecordingEmailSender, the same
/// way EmailVerificationEndpointsTests does it, so a test can read the link a
/// real recipient would click.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PasswordResetEndpointTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public PasswordResetEndpointTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> CreateHost(
        IDictionary<string, string?>? extraSettings = null,
        CapturedLogs? logs = null) =>
        SessionTestHost.Create(
            _factory,
            _database,
            settings: extraSettings,
            services: services =>
            {
                services.AddSingleton<IEmailSender, RecordingEmailSender>();

                if (logs is not null)
                {
                    services.AddSingleton<ILoggerProvider>(logs);
                }
            });

    private static (string UserId, string Token) ExtractResetLink(string emailBody)
    {
        var url = Regex.Match(emailBody, @"https?://\S+").Value;
        Assert.NotEmpty(url);

        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        return (query["userId"].ToString(), query["token"].ToString());
    }

    [RequiresDatabaseFact]
    public async Task Forgot_password_sends_a_reset_email_with_a_working_link_for_a_known_account()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        var response = await client.PostAsJsonAsync(
            "/forgot-password", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        Assert.Contains("/reset-password", sent.Body);

        var (userId, token) = ExtractResetLink(sent.Body);
        Assert.NotEmpty(userId);
        Assert.NotEmpty(token);
    }

    [RequiresDatabaseFact]
    public async Task Forgot_password_answers_ok_but_sends_nothing_for_an_unknown_address()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("nobody");

        var response = await client.PostAsJsonAsync(
            "/forgot-password", new ForgotPasswordRequest(email));

        // Never distinguishable from a hit, the whole point of security rule 3.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(emails.Sent, m => m.To == email);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_with_a_fresh_token_changes_the_password()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        const string newPassword = "Nowe-Haslo1";
        var response = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, newPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The old password no longer works, the new one does.
        var oldLogin = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_invalidates_every_active_session()
    {
        var host = CreateHost();
        var client = SessionTestHost.RawClient(host);
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        var login = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));
        var cookie = SessionTestHost.SessionCookie(login);

        // The cookie works before the reset.
        var meBefore = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie));
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, "Nowe-Haslo1"));

        // Same cookie the browser would still be holding, now worthless.
        var meAfter = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_rejects_a_token_that_has_already_been_used()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        var first = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, "Nowe-Haslo1"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same link, used a second time (e.g. a stale browser tab).
        var second = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, "Inne-Haslo2"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_rejects_an_expired_token()
    {
        // TokenLifespan is added to the token's own creation time and compared
        // against "now" at verification time: a lifespan of zero hours means
        // the token is already expired by the time it is checked, without
        // this test waiting on a real clock.
        var host = CreateHost(new Dictionary<string, string?>
        {
            ["PasswordReset:TokenLifetimeHours"] = "0",
        });
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        var response = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, "Nowe-Haslo1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_rejects_a_malformed_token()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("reset");
        var user = await SessionTestHost.CreateAccountAsync(host, email);

        var response = await client.PostAsJsonAsync(
            "/reset-password",
            new ResetPasswordRequest(
                user.Id.ToString(), "not-a-valid-base64-token!!", "Nowe-Haslo1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_rejects_an_unknown_user_id()
    {
        var host = CreateHost();
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/reset-password",
            new ResetPasswordRequest(Guid.NewGuid().ToString(), "anything", "Nowe-Haslo1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_rejects_a_user_id_that_is_not_a_guid()
    {
        // UserManager.FindByIdAsync converts the id straight to a Guid and
        // throws FormatException on anything else - a link parameter has to
        // survive being hand edited, not just a syntactically valid but
        // unknown id.
        var host = CreateHost();
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/reset-password",
            new ResetPasswordRequest("not-a-guid", "anything", "Nowe-Haslo1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseTheory]
    [InlineData("Krot1!", "Hasło musi zawierać co najmniej 8 znaków.")]
    [InlineData("Bez-Cyfry", "Hasło musi zawierać co najmniej jedną cyfrę.")]
    [InlineData("bez-wielkiej1", "Hasło musi zawierać co najmniej jedną wielką literę.")]
    public async Task A_new_password_failing_the_policy_is_refused_in_polish(
        string newPassword,
        string expected)
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        var response = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, newPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_password_reaches_neither_the_body_nor_the_log()
    {
        // AGENTS.md security rule 4: the accepted path is the dangerous one,
        // because the request was processed and anything that logs what it
        // processed logs a credential.
        var logs = new CapturedLogs();
        var host = CreateHost(logs: logs);
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = SessionTestHost.Email("reset");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest(email));
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractResetLink(sent.Body);

        const string newPassword = "Nowe-Haslo1";
        var response = await client.PostAsJsonAsync(
            "/reset-password", new ResetPasswordRequest(userId, token, newPassword));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(newPassword, body);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(newPassword));
    }
}
