using System.Diagnostics;
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
    public async Task A_locked_deactivated_account_does_not_answer_faster_than_an_unknown_one()
    {
        // Arrange
        // The half of hiding a deactivated account that the body and the
        // status cannot show. A locked account never reaches the password
        // hash (CheckPasswordSignInAsync checks the lockout first), so
        // without the deliberate burn this path answers after one index
        // lookup while an unknown address still pays for a full key
        // derivation: same message, and a stopwatch that reads "this address
        // used to have an account".
        //
        // Medians of nine samples, and the threshold is loose on purpose.
        // Measured on this stack the ratio is ~0.9 with the burn and ~0.04
        // without it (3 ms against 100 ms), so 0.4 sits an order of
        // magnitude clear of both, which is what keeps a timing test from
        // becoming the flaky one somebody disables.
        var host = Host(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "500",
        });
        var client = host.CreateClient();
        var deactivated = SessionTestHost.Email("dezaktywowany-czas");
        await SessionTestHost.CreateAccountAsync(host, deactivated, active: false);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            await Login(client, deactivated, "Zle-Haslo1!");
        }

        // The first requests on a cold host pay for JIT and for the first
        // hash, which is noise this test is not about.
        for (var warmup = 0; warmup < 3; warmup++)
        {
            await Login(client, SessionTestHost.Email("rozgrzewka"), "Zle-Haslo1!");
            await Login(client, deactivated, "Zle-Haslo1!");
        }

        // Act
        var unknown = new List<double>();
        var locked = new List<double>();

        for (var sample = 0; sample < 9; sample++)
        {
            var stopwatch = Stopwatch.StartNew();
            await Login(client, SessionTestHost.Email("nie-istnieje"), "Zle-Haslo1!");
            unknown.Add(stopwatch.Elapsed.TotalMilliseconds);

            stopwatch = Stopwatch.StartNew();
            await Login(client, deactivated, "Zle-Haslo1!");
            locked.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        unknown.Sort();
        locked.Sort();

        // Assert
        var ratio = locked[4] / unknown[4];

        Assert.True(
            ratio >= 0.4,
            $"A locked deactivated account answered {ratio:F3} of the time an "
            + "unknown address took, so the two are distinguishable by "
            + $"stopwatch (deactivated {locked[4]:F1} ms, unknown {unknown[4]:F1} ms).");
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
