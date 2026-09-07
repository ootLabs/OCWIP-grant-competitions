using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// POST /register over real HTTP against a real PostgreSQL.
///
/// The card's own acceptance criterion is that the answer for a taken address is
/// identical to the answer for a free one (security rule 3). The version of this
/// endpoint that shipped in PR #10 answered 201 for a free address and 400 for a
/// taken one, with a body reading "Konto zostało utworzone.", and it passed
/// review because its tests went straight to EF and never issued a request.
/// That is the gap this class exists to close, so the assertions here are about
/// what a caller can OBSERVE, not about what the service returns.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationEndpointTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private const string ValidPassword = "Tajne-Haslo1";

    private readonly PostgresDatabaseFixture _database;
    private readonly OcwipWebApplicationFactory _factory;

    public RegistrationEndpointTests(
        PostgresDatabaseFixture database,
        OcwipWebApplicationFactory factory)
    {
        _database = database;
        _factory = factory;
    }

    private static string Email(string label) =>
        $"{label}-{Guid.NewGuid():N}@example.org";

    private HttpClient Client(CapturedLogs? logs = null) =>
        _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres", _database.ConnectionString!);

                if (logs is not null)
                {
                    builder.ConfigureLogging(
                        logging => logging.AddProvider(logs));
                }
            })
            .CreateClient();

    private static Task<HttpResponseMessage> Register(
        HttpClient client,
        string email,
        string password = ValidPassword) =>
        client.PostAsJsonAsync(
            "/register",
            new RegisterRequest(email, password, "Adam", "Testowy"));

    /// <summary>
    /// Everything a caller can read off a response, minus the headers that
    /// differ between any two requests by definition. Comparing whole
    /// fingerprints rather than status codes is deliberate: a difference in the
    /// body or in Content-Length leaks existence just as well as a status.
    /// </summary>
    private static async Task<string> Fingerprint(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .Where(header => !string.Equals(
                header.Key, "Date", StringComparison.OrdinalIgnoreCase))
            .OrderBy(header => header.Key, StringComparer.Ordinal)
            .Select(header => $"{header.Key}: {string.Join(",", header.Value)}");

        return $"{(int)response.StatusCode}\n{string.Join("\n", headers)}\n{body}";
    }

    [RequiresDatabaseFact]
    public async Task A_free_address_creates_an_unconfirmed_applicant_account()
    {
        // Arrange
        var email = Email("wolny");

        // Act
        var response = await Register(Client(), email);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(x => x.Email == email);

        Assert.Equal(Role.Applicant, stored.Role);
        Assert.False(stored.EmailConfirmed);
        Assert.True(stored.IsActive);
        Assert.Null(stored.DeactivatedAt);
        Assert.Null(stored.Pesel);
        Assert.Null(stored.EntityId);

        // A hash, and demonstrably not the password.
        Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));
        Assert.DoesNotContain(ValidPassword, stored.PasswordHash);

        // UserManager owns the normalized columns, so registration must not
        // have left them for something else to fill in.
        Assert.Equal(email.ToUpperInvariant(), stored.NormalizedEmail);
        Assert.Equal(email, stored.UserName);
    }

    [RequiresDatabaseFact]
    public async Task A_taken_address_is_indistinguishable_from_a_free_one()
    {
        // Arrange
        var client = Client();
        var taken = Email("zajety");
        var free = Email("wolny");

        var first = await Register(client, taken);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Act
        var onTaken = await Register(client, taken);
        var onFree = await Register(client, free);

        // Assert
        // Status, every stable header and the body, byte for byte. This is the
        // card's acceptance criterion and the regression for PR #10.
        Assert.Equal(await Fingerprint(onFree), await Fingerprint(onTaken));

        // And the second attempt really did not write anything.
        await using var context = _database.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync(x => x.Email == taken));
    }

    [RequiresDatabaseFact]
    public async Task An_address_differing_only_in_case_is_the_same_account()
    {
        // Arrange
        // Uniqueness stands on normalized_email since T-12.0, so this is the
        // same address, and the answer has to stay indistinguishable.
        var client = Client();
        var email = Email("wielkosc-liter");

        await Register(client, email);

        // Act
        var again = await Register(client, email.ToUpperInvariant());
        var onFree = await Register(client, Email("wolny"));

        // Assert
        Assert.Equal(await Fingerprint(onFree), await Fingerprint(again));

        await using var context = _database.CreateContext();
        var normalized = email.ToUpperInvariant();
        Assert.Equal(
            1,
            await context.Users.CountAsync(x => x.NormalizedEmail == normalized));
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_account_keeps_its_address()
    {
        // Arrange
        // Pins a KNOWN TRAP rather than desired behaviour. A deactivated account
        // keeps its address forever (the unique index covers every row), so
        // registering on it answers exactly like success while the person can
        // never get in. Security rule 3 is satisfied and the human is not: the
        // supported way back is reactivation, which is an open point in
        // docs/model-danych.md and has its own card. This test is here so that
        // stays a recorded decision instead of a surprise.
        var client = Client();
        var email = Email("dezaktywowany");
        await Register(client, email);

        await using (var deactivate = _database.CreateContext())
        {
            var account = await deactivate.Users.SingleAsync(x => x.Email == email);
            account.IsActive = false;
            account.DeactivatedAt = DateTimeOffset.UtcNow;
            await deactivate.SaveChangesAsync();
        }

        // Act
        var again = await Register(client, email);
        var onFree = await Register(client, Email("wolny"));

        // Assert
        Assert.Equal(await Fingerprint(onFree), await Fingerprint(again));

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(x => x.Email == email);
        Assert.False(stored.IsActive);
    }

    [RequiresDatabaseFact]
    public async Task A_duplicate_that_slips_past_the_validator_looks_the_same()
    {
        // Arrange
        // The SECOND door to "that address is taken", and the one a real
        // deployment will actually walk through. UserManager checks for a
        // duplicate with a SELECT before its INSERT, and that check loses the
        // race against a registration arriving in the same moment: the unique
        // index answers with 23505 instead, which AccountService catches.
        //
        // Removing the validator is how that path becomes deterministic.
        // Waiting for a genuine race would give a test that passes for reasons
        // nobody controls, and the catch block would otherwise be covered by
        // nothing at all.
        var racing = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres", _database.ConnectionString!);
                builder.ConfigureServices(
                    services => services.RemoveAll<IUserValidator<User>>());
            })
            .CreateClient();

        var email = Email("wyscig");
        var first = await Register(racing, email);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Act
        var again = await Register(racing, email);
        var onFree = await Register(racing, Email("wolny"));

        // Assert
        Assert.Equal(await Fingerprint(onFree), await Fingerprint(again));

        await using var context = _database.CreateContext();
        Assert.Equal(1, await context.Users.CountAsync(x => x.Email == email));
    }

    [RequiresDatabaseTheory]
    [InlineData("Krot1!", "Hasło musi zawierać co najmniej 8 znaków.")]
    [InlineData("Tajne-Haslo", "Hasło musi zawierać co najmniej jedną cyfrę.")]
    [InlineData(
        "tajne-haslo1", "Hasło musi zawierać co najmniej jedną wielką literę.")]
    [InlineData(
        "TAJNE-HASLO1", "Hasło musi zawierać co najmniej jedną małą literę.")]
    [InlineData(
        "TajneHaslo1", "Hasło musi zawierać co najmniej jeden znak specjalny.")]
    public async Task A_password_failing_the_policy_is_refused_in_polish(
        string password,
        string expected)
    {
        // Act
        var response = await Register(Client(), Email("slabe-haslo"), password);

        // Assert
        // A refused password is the one answer that MAY differ, because it says
        // nothing about whether an account exists.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseTheory]
    [InlineData(ValidPassword)]
    [InlineData("krotkie")]
    public async Task A_password_reaches_neither_the_body_nor_the_log(
        string password)
    {
        // Arrange
        // AGENTS.md security rule 4. Both paths are checked, because the
        // accepted one is the dangerous one: the request was processed, so
        // anything that logs what it processed logs a credential.
        var logs = new CapturedLogs();

        // Act
        var response = await Register(Client(logs), Email("haslo"), password);

        // Assert
        Assert.DoesNotContain(
            password, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(password, logs.Text);
    }

    [RequiresDatabaseTheory]
    [InlineData("nie-jest-adresem")]
    // Parsed and round tripped by MailAddress, and refused by the
    // EmailAddressAttribute Identity's UserValidator runs on its own. Without
    // the edge applying the same rule, this answered 400 with Identity's
    // English "Email '...' is invalid." reported against the PASSWORD field.
    [InlineData("\"a@b\"@example.org")]
    public async Task A_malformed_address_is_refused_before_any_write(string email)
    {
        // Act
        var response = await Register(Client(), email);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("To nie jest poprawny adres e-mail.", body);
        // Against the field it is about, and in Polish. Both halves failed
        // before the edge learned Identity's rule.
        Assert.DoesNotContain("password", body);
        Assert.DoesNotContain("is invalid", body);
    }

    [RequiresDatabaseFact]
    public async Task An_address_pasted_with_whitespace_is_the_same_account()
    {
        // Arrange
        // The address is stored trimmed, so the normalized copy is trimmed too.
        // Untrimmed, " adam@x.pl" and "adam@x.pl" normalize to two different
        // values and the unique index lets one address hold two accounts.
        var client = Client();
        var email = Email("spacje");

        var first = await Register(client, $"  {email}  ");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Act
        var again = await Register(client, email);
        var onFree = await Register(client, Email("wolny"));

        // Assert
        Assert.Equal(await Fingerprint(onFree), await Fingerprint(again));

        await using var context = _database.CreateContext();
        var stored = await context.Users.SingleAsync(x => x.Email == email);
        Assert.Equal(email, stored.UserName);
        Assert.Equal(email.ToUpperInvariant(), stored.NormalizedEmail);
    }

    [Fact]
    public async Task Registration_answers_503_on_a_host_with_no_database()
    {
        // Arrange
        // A supported way to run the API rather than a misconfiguration: the
        // health probes answer without a database (HealthEndpointsTests), so
        // the write path has to answer too. It used to reach
        // GetRequiredService and return a 500 whose body named the internal
        // service type, which is exactly what /health/db is careful not to do.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseSetting(
                "ConnectionStrings:Postgres", string.Empty))
            .CreateClient();

        // Act
        var response = await Register(client, "adam@example.org");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Rejestracja jest chwilowo niedostępna.", body);
        Assert.DoesNotContain("IAccountService", body);
    }
}
