using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Contracts;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// POST /login, POST /logout and GET /me over real HTTP against a real
/// PostgreSQL (T-12.3).
///
/// The assertions are about what a CALLER can observe, not about what the
/// service returns, for the same reason RegistrationEndpointTests is written
/// that way: the two rules this card can break, one message for every credential
/// failure and a logout that really ends the session, are both invisible from
/// inside the service.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public SessionEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> Host() =>
        SessionTestHost.Create(_factory, _database);

    private static Task<HttpResponseMessage> Login(
        HttpClient client,
        string email,
        string? password = null,
        string? returnUrl = null) =>
        client.PostAsJsonAsync(
            "/login",
            new LoginRequest(email, password ?? SessionTestHost.Password, returnUrl));

    /// <summary>
    /// Everything a caller can read off a response, minus what differs between
    /// any two requests by definition: the date and the trace identifier
    /// ProblemDetails stamps on every problem body. Set-Cookie is not excluded,
    /// because a response that quietly opened a session while claiming to have
    /// refused one is precisely the kind of difference worth failing on.
    /// </summary>
    private static async Task<string> Fingerprint(HttpResponseMessage response)
    {
        var body = TraceId.Replace(
            await response.Content.ReadAsStringAsync(), "\"traceId\":\"?\"");

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .Where(header => !string.Equals(
                header.Key, "Date", StringComparison.OrdinalIgnoreCase))
            .OrderBy(header => header.Key, StringComparer.Ordinal)
            .Select(header => $"{header.Key}: {string.Join(",", header.Value)}");

        return $"{(int)response.StatusCode}\n{string.Join("\n", headers)}\n{body}";
    }

    private static readonly Regex TraceId =
        new("\"traceId\":\"[^\"]*\"", RegexOptions.Compiled);

    [RequiresDatabaseFact]
    public async Task Correct_credentials_open_a_session_and_say_where_to_go()
    {
        // Arrange
        var host = Host();
        var email = SessionTestHost.Email("wnioskodawca");
        await SessionTestHost.CreateAccountAsync(host, email);
        var client = host.CreateClient();

        // Act
        var response = await Login(client, email);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal(email, session.Email);
        Assert.Equal(Role.Applicant, session.Role);
        Assert.Equal(LoginLandingPath.Applicant, session.RedirectPath);

        // The session travels in the cookie and nowhere else: nothing in the
        // body is a credential, so a front that logs the response logs nothing
        // worth stealing.
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("ocwip.session=", StringComparison.Ordinal));

        var me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        // By NAME on the wire, not by ordinal. With the default serializer this
        // reads "role":0, which makes the order of the values in Models/Role.cs
        // part of the API contract and turns inserting a role into a silent
        // reassignment for every client holding the old numbers.
        Assert.Contains(
            "\"role\":\"Applicant\"", await me.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task An_operator_lands_on_the_operator_panel()
    {
        // Arrange
        // The card's role criterion. The role is a COLUMN here, not an Identity
        // role table, so if the claims factory ever stops copying it the
        // destination silently becomes the applicant's panel rather than
        // failing loudly.
        var host = Host();
        var email = SessionTestHost.Email("operator");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Operator);

        // Act
        var response = await Login(host.CreateClient(), email);

        // Assert
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal(Role.Operator, session.Role);
        Assert.Equal(LoginLandingPath.Operator, session.RedirectPath);
    }

    [RequiresDatabaseFact]
    public async Task A_reviewer_lands_on_the_reviewer_panel()
    {
        // Arrange
        var host = Host();
        var email = SessionTestHost.Email("recenzent");
        await SessionTestHost.CreateAccountAsync(host, email, Role.Reviewer);

        // Act
        var response = await Login(host.CreateClient(), email);

        // Assert
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal(LoginLandingPath.Reviewer, session.RedirectPath);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_returns_to_the_competition_page_they_came_from()
    {
        // Arrange
        // The report's rule from step 3.1: someone who clicked "Wypełnij
        // wniosek" and had to sign in first comes back to THAT competition, not
        // to a generic dashboard.
        var host = Host();
        var email = SessionTestHost.Email("wraca");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        var response = await Login(
            host.CreateClient(), email, returnUrl: "/konkursy/17");

        // Assert
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal("/konkursy/17", session.RedirectPath);
    }

    [RequiresDatabaseFact]
    public async Task A_destination_outside_the_site_is_refused()
    {
        // Arrange
        // The same field, as an open redirect: a link to /login with a crafted
        // returnUrl is how a phishing page gets to be reached from our own
        // domain, right after a real sign in.
        var host = Host();
        var email = SessionTestHost.Email("open-redirect");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        var response = await Login(
            host.CreateClient(), email, returnUrl: "https://evil.example/zaloguj");

        // Assert
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Equal(LoginLandingPath.Applicant, session.RedirectPath);
    }

    [RequiresDatabaseFact]
    public async Task A_wrong_password_looks_exactly_like_an_unknown_address()
    {
        // Arrange
        // The card's first rule: telling the two apart is how an outsider finds
        // out who has an account here. Whole fingerprints rather than statuses,
        // because a difference in the body or in Content-Length leaks just as
        // well as a difference in the code.
        var host = Host();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("istnieje");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        var wrongPassword = await Login(client, email, "Zle-Haslo1!");
        var unknownAddress = await Login(
            client, SessionTestHost.Email("nie-istnieje"), "Zle-Haslo1!");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(
            await Fingerprint(unknownAddress), await Fingerprint(wrongPassword));

        Assert.Contains(
            SessionEndpoints.InvalidCredentials,
            await wrongPassword.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task An_unconfirmed_account_is_told_so_only_after_the_right_password()
    {
        // Arrange
        // The card wants a readable message for an unconfirmed address and the
        // security rules forbid revealing which addresses have accounts. Both
        // hold at once precisely because the message sits BEHIND the password
        // check: the only person who can see it already knows the password.
        var host = Host();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("niepotwierdzony");
        await SessionTestHost.CreateAccountAsync(host, email, confirmed: false);

        // Act
        var withWrongPassword = await Login(client, email, "Zle-Haslo1!");
        var unknownAddress = await Login(
            client, SessionTestHost.Email("nie-istnieje"), "Zle-Haslo1!");
        var withRightPassword = await Login(client, email);

        // Assert
        // Before the password is right, an unconfirmed account is
        // indistinguishable from no account at all.
        Assert.Equal(
            await Fingerprint(unknownAddress),
            await Fingerprint(withWrongPassword));

        // 403 and not 401: the credentials were correct, so retyping them
        // cannot help, and a browser prompted to ask again would only confuse
        // the person.
        Assert.Equal(HttpStatusCode.Forbidden, withRightPassword.StatusCode);
        Assert.Contains(
            "Potwierdź swój adres e-mail",
            await withRightPassword.Content.ReadAsStringAsync());

        // And no session was opened.
        Assert.DoesNotContain("Set-Cookie", withRightPassword.Headers.Select(h => h.Key));
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_account_answers_like_a_wrong_password()
    {
        // Arrange
        // We never hard delete (security rule 5), so "this account is gone" and
        // "this account never existed" have to look identical from outside.
        // Otherwise soft delete becomes a way to enumerate former users.
        var host = Host();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("dezaktywowany");
        await SessionTestHost.CreateAccountAsync(host, email, active: false);

        // Act
        var deactivated = await Login(client, email);
        var unknownAddress = await Login(client, SessionTestHost.Email("nie-istnieje"));

        // Assert
        Assert.Equal(
            await Fingerprint(unknownAddress), await Fingerprint(deactivated));
    }

    [RequiresDatabaseFact]
    public async Task Logging_out_kills_a_copy_of_the_cookie_too()
    {
        // Arrange
        // The whole point of the card's server side requirement. Deleting the
        // cookie in this browser does nothing to the copy someone took off a
        // shared machine, so the test replays the captured cookie AFTER the
        // logout. Without the security stamp rotation in SessionService this
        // request answers 200 and the session lives on.
        var host = Host();
        var client = SessionTestHost.RawClient(host);
        var email = SessionTestHost.Email("wylogowanie");
        await SessionTestHost.CreateAccountAsync(host, email);

        var login = await Login(client, email);
        var cookie = SessionTestHost.SessionCookie(login);

        var before = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Act
        var logout = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Post, "/logout", cookie));

        // Assert
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var after = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Logging_out_without_a_session_is_not_an_error()
    {
        // Arrange
        // Idempotent on purpose: a caller asking for the state it is already in
        // got what it wanted. Answering 401 here teaches the front to treat a
        // successful logout as a failure and show an error to someone who is,
        // in fact, logged out.
        var client = Host().CreateClient();

        // Act
        var response = await client.PostAsync("/logout", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_account_deactivated_mid_session_stops_being_let_in()
    {
        // Arrange
        // The cookie stays cryptographically valid, so nothing about it says
        // the account behind it was switched off two minutes ago. /me reads the
        // row, not just the ticket.
        var host = Host();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("wylaczony-w-trakcie");
        var user = await SessionTestHost.CreateAccountAsync(host, email);

        await Login(client, email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me")).StatusCode);

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Users.FindAsync(user.Id);
            stored!.IsActive = false;
            stored.DeactivatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_unauthenticated_request_gets_401_json_and_not_a_login_page()
    {
        // Arrange
        // Identity's default answer is 302 to /Account/Login, a page that does
        // not exist in this product. A fetch would follow it, read 200 and some
        // HTML, and conclude that everything is fine.
        var client = SessionTestHost.RawClient(Host());

        // Act
        var response = await client.GetAsync("/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [RequiresDatabaseTheory]
    [InlineData(SessionTestHost.Password)]
    [InlineData("Zle-Haslo1!")]
    public async Task A_password_reaches_neither_the_body_nor_the_log(string password)
    {
        // Arrange
        // Security rule 4, checked on both paths. The accepted one is the
        // dangerous one: the request was processed, so anything that logs what
        // it processed logs a credential.
        var logs = new CapturedLogs();
        var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Postgres", _database.ConnectionString);
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

        var email = SessionTestHost.Email("haslo");
        await SessionTestHost.CreateAccountAsync(host, email);
        var client = host.CreateClient();

        // Act
        var response = await Login(client, email, password);

        // Assert
        Assert.DoesNotContain(password, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(password, logs.Text);
    }

    [Fact]
    public async Task Login_answers_503_on_a_host_with_no_database()
    {
        // Arrange
        // A supported way to run the API, not a misconfiguration: the health
        // probes answer without a database, so the write paths have to answer
        // too, and with a message that does not name an internal type.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting(
                "ConnectionStrings:Postgres", string.Empty))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/login", new LoginRequest("ada@example.org", "Str0ng!Passw0rd"));

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(SessionEndpoints.Unavailable, body);
        Assert.DoesNotContain("ISessionService", body);
    }

    [Fact]
    public async Task The_protected_endpoint_still_routes_on_a_host_with_no_database()
    {
        // Arrange
        // RequireAuthorization on an endpoint whose authentication scheme was
        // never registered takes down routing for the WHOLE application, not
        // just that endpoint, which is why the cookie handler is registered
        // outside the connection string check in Program.cs.
        var client = SessionTestHost.RawClient(_factory
            .WithWebHostBuilder(builder => builder.UseSetting(
                "ConnectionStrings:Postgres", string.Empty)));

        // Act
        var me = await client.GetAsync("/me");
        var health = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
