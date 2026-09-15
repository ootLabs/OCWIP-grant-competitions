using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// What the session cookie is worth and for how long (T-12.3).
///
/// Split off from SessionEndpointsTests because these tests need their own host
/// per case: a clock they can move, or a cookie policy that differs from the
/// one the rest of the suite runs under.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionLifetimeTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public SessionLifetimeTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    /// <summary>
    /// A clock the test moves by hand. The alternative is a real wait, and a
    /// suite that sleeps for the length of a session is a suite somebody turns
    /// off.
    /// </summary>
    private sealed class MovableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    [RequiresDatabaseFact]
    public async Task A_session_older_than_its_lifetime_is_refused()
    {
        // Arrange
        var clock = new MovableClock(DateTimeOffset.UtcNow);
        var host = SessionTestHost.Create(
            _factory,
            _database,
            settings: new Dictionary<string, string?>
            {
                ["Auth:SessionLifetimeHours"] = "2",
            },
            services: services => services.Configure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = clock));

        var client = SessionTestHost.RawClient(host);
        var email = SessionTestHost.Email("wygasla-sesja");
        await SessionTestHost.CreateAccountAsync(host, email);

        var login = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));
        var cookie = SessionTestHost.SessionCookie(login);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.SendAsync(
                SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie))).StatusCode);

        // Act
        // Past the configured two hours. The cookie itself is untouched, which
        // is the point: a ticket that never went stale would be a session
        // without an end, and the card asks for a defined lifetime.
        clock.Advance(TimeSpan.FromHours(3));

        var response = await client.SendAsync(
            SessionTestHost.WithCookie(HttpMethod.Get, "/me", cookie));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_session_in_use_slides_instead_of_expiring_mid_work()
    {
        // Arrange
        // Sliding expiration measures inactivity, not the length of the working
        // day. Without it an operator reviewing applications is thrown out
        // mid review at a fixed hour, which is the moment they can least afford
        // it: right after the intake closes.
        var clock = new MovableClock(DateTimeOffset.UtcNow);
        var host = SessionTestHost.Create(
            _factory,
            _database,
            settings: new Dictionary<string, string?>
            {
                ["Auth:SessionLifetimeHours"] = "2",
            },
            services: services => services.Configure<CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = clock));

        var client = host.CreateClient();
        var email = SessionTestHost.Email("suwana-sesja");
        await SessionTestHost.CreateAccountAsync(host, email);

        await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));

        // Act
        // Three hours in total, but never more than ninety minutes of silence.
        clock.Advance(TimeSpan.FromMinutes(90));
        var midway = await client.GetAsync("/me");

        clock.Advance(TimeSpan.FromMinutes(90));
        var later = await client.GetAsync("/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, midway.StatusCode);
        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_cookie_is_httponly_and_stays_on_this_site()
    {
        // Arrange
        // HttpOnly is why this is a cookie and not a token in localStorage:
        // script must not be able to read the session, so an XSS bug costs a
        // page and not every account. SameSite is what keeps another site from
        // riding along on it.
        var host = SessionTestHost.Create(_factory, _database);
        var client = SessionTestHost.RawClient(host);
        var email = SessionTestHost.Email("ciasteczko");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        var response = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));

        // Assert
        var header = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("ocwip.session=", StringComparison.Ordinal));

        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
    }

    [RequiresDatabaseFact]
    public async Task Outside_development_the_cookie_refuses_plain_http()
    {
        // Arrange
        // Secure is the default everywhere except a local stack on plain http,
        // where the browser would drop the cookie and nothing would work at
        // all. The test host runs on http, so the flag is set explicitly rather
        // than inferred, which is exactly how a deployment sets it too.
        var host = SessionTestHost.Create(
            _factory,
            _database,
            settings: new Dictionary<string, string?>
            {
                ["Auth:SecureCookie"] = "true",
            });

        var client = SessionTestHost.RawClient(host);
        var email = SessionTestHost.Email("secure");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        var response = await client.PostAsJsonAsync(
            "/login", new LoginRequest(email, SessionTestHost.Password));

        // Assert
        var header = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("ocwip.session=", StringComparison.Ordinal));

        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
    }
}
