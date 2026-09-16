using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The account half of brute force protection (T-12.5): repeated wrong
/// passwords against ONE address lock it, regardless of which address the
/// attempts came from. Separate from SessionEndpointsTests, which is already
/// at the size this file would have pushed it past, and from
/// RateLimitingTests, which covers the IP half.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class LoginLockoutTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public LoginLockoutTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> Host(
        IDictionary<string, string?>? settings = null) =>
        SessionTestHost.Create(_factory, _database, settings: settings);

    private static Task<HttpResponseMessage> Login(
        HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/login", new { email, password });

    [RequiresDatabaseFact]
    public async Task Five_wrong_passwords_lock_the_account_with_a_readable_message()
    {
        // Arrange
        // A high enough rate limit that the IP half of this card does not
        // interfere with a test of the account half.
        var host = Host(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "20",
        });
        var client = host.CreateClient();
        var email = SessionTestHost.Email("bruteforce");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await Login(client, email, "Zle-Haslo1!");
        }

        // Assert
        // The fifth wrong password is the one that crosses
        // MaxFailedAccessAttempts, so it is already answered as locked out,
        // not as one more wrong password.
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Contains("tymczasowo zablokowane", await last.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_correct_password_during_lockout_is_still_refused()
    {
        // Arrange
        // The point of PreSignInCheck running before the password: an
        // attacker who eventually guesses right during the lockout window
        // must not slip through on the one attempt that happened to be
        // correct.
        var host = Host(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "20",
        });
        var client = host.CreateClient();
        var email = SessionTestHost.Email("bruteforce");
        await SessionTestHost.CreateAccountAsync(host, email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Login(client, email, "Zle-Haslo1!");
        }

        // Act
        var withRightPassword = await Login(client, email, SessionTestHost.Password);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, withRightPassword.StatusCode);
        Assert.DoesNotContain(
            "Set-Cookie", withRightPassword.Headers.Select(h => h.Key));
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_account_never_admits_to_being_locked_out()
    {
        // Arrange
        // Security rule 5: we do not hard delete, so a deactivated account has
        // to keep answering like an address nobody ever registered. Without
        // the IsActive guard on the lockout branch, five attempts turn soft
        // delete into a way to enumerate former users, and the single attempt
        // in SessionEndpointsTests would not have noticed.
        var host = Host(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "20",
        });
        var client = host.CreateClient();
        var deactivated = SessionTestHost.Email("dezaktywowany");
        await SessionTestHost.CreateAccountAsync(host, deactivated, active: false);

        // Act
        HttpResponseMessage? onDeactivated = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            onDeactivated = await Login(client, deactivated, "Zle-Haslo1!");
        }

        var onUnknown = await Login(
            client, SessionTestHost.Email("nie-istnieje"), "Zle-Haslo1!");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, onDeactivated!.StatusCode);
        Assert.Equal(onUnknown.StatusCode, onDeactivated.StatusCode);
        Assert.DoesNotContain(
            "zablokowane", await onDeactivated.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task The_lockout_answer_carries_a_retry_after_header()
    {
        // Arrange
        // /login now has two different 429s, one clearing in seconds and one
        // in fifteen minutes. Without this header a client cannot tell them
        // apart and retries straight into the long one.
        var host = Host(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "20",
        });
        var client = host.CreateClient();
        var email = SessionTestHost.Email("retry-after");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await Login(client, email, "Zle-Haslo1!");
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter);
    }

    [RequiresDatabaseFact]
    public async Task Failed_logins_are_logged_without_the_password()
    {
        // Arrange
        var logs = new CapturedLogs();
        var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Postgres", _database.ConnectionString!);
            builder.UseSetting("RateLimiting:PermitLimit", "20");
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        var client = host.CreateClient();
        var email = SessionTestHost.Email("logged");
        await SessionTestHost.CreateAccountAsync(host, email);

        // Act
        const string wrongPassword = "Zle-Haslo1!";
        await Login(client, email, wrongPassword);
        await Login(client, SessionTestHost.Email("nieznany"), wrongPassword);

        // Assert
        Assert.Contains(logs.Messages, m => m.Contains("Login failed"));
        Assert.DoesNotContain(wrongPassword, logs.Text);
        Assert.DoesNotContain(SessionTestHost.Password, logs.Text);
    }
}
