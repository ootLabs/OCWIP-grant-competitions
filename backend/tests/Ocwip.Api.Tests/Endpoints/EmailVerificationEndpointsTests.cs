using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// End to end coverage for /register, /verify-email and /resend-verification
/// against a real PostgreSQL database - the same database the app itself would
/// talk to, per OcwipWebApplicationFactory. IEmailSender is swapped for
/// RecordingEmailSender so a test can inspect what would have been sent
/// without a real mail transport.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailVerificationEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    // Meets every rule in IdentityConfiguration.AddIdentityConfiguration.
    private const string ValidPassword = "Str0ng!Passw0rd";

    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public EmailVerificationEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> CreateHost(
        IDictionary<string, string?>? extraSettings = null) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _database.ConnectionString);

            if (extraSettings is not null)
            {
                foreach (var (key, value) in extraSettings)
                {
                    builder.UseSetting(key, value);
                }
            }

            // Real IEmailSender only logs, so tests capture what it would have
            // sent instead. Singleton: it has to outlive the per-request scope
            // the app resolves IEmailSender from.
            builder.ConfigureServices(services =>
                services.AddSingleton<IEmailSender, RecordingEmailSender>());
        });

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email) =>
        await client.PostAsJsonAsync("/register", new
        {
            email,
            password = ValidPassword,
            firstName = "Ada",
            lastName = "Testowa",
        });

    private static async Task<User> CreateUnconfirmedUserAsync(
        IServiceProvider services,
        string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = "Adam",
            LastName = "Testowy",
            Role = Role.Applicant,
            Pesel = "90010112345",
        };

        var result = await userManager.CreateAsync(user, ValidPassword);
        Assert.True(result.Succeeded);

        return user;
    }

    private static async Task<bool> IsEmailConfirmedAsync(
        IServiceProvider services,
        string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!.EmailConfirmed;
    }

    /// <summary>
    /// Parses the link a real recipient would click out of the plain text
    /// verification email, the same way a browser would.
    /// </summary>
    private static (string UserId, string Token) ExtractVerificationLink(string emailBody)
    {
        var url = Regex.Match(emailBody, @"https?://\S+").Value;
        Assert.NotEmpty(url);

        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        return (query["userId"].ToString(), query["token"].ToString());
    }

    [RequiresDatabaseFact]
    public async Task Registering_sends_a_verification_email_with_a_working_link()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        var response = await RegisterAsync(client, email);

        // /register answers 202 since the merge with dev's RegistrationResult
        // contract (docs/log.md, 2026-09-07): the same status for a freshly
        // created account and for an address that already has one.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var sent = Assert.Single(emails.Sent, m => m.To == email);
        Assert.Contains("/verify-email", sent.Body);

        var (userId, token) = ExtractVerificationLink(sent.Body);
        Assert.NotEmpty(userId);
        Assert.NotEmpty(token);
    }

    [RequiresDatabaseFact]
    public async Task Registering_uses_the_configured_frontend_base_url()
    {
        var host = CreateHost(new Dictionary<string, string?>
        {
            ["EmailVerification:FrontendBaseUrl"] = "https://app.example.test",
        });
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        var sent = Assert.Single(emails.Sent, m => m.To == email);
        Assert.Contains("https://app.example.test/verify-email", sent.Body);
    }

    [RequiresDatabaseFact]
    public async Task Verify_email_confirms_a_freshly_registered_user()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractVerificationLink(sent.Body);

        var response = await client.PostAsJsonAsync("/verify-email", new { userId, token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await IsEmailConfirmedAsync(host.Services, email));
    }

    [RequiresDatabaseFact]
    public async Task Verify_email_rejects_a_malformed_token()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, _) = ExtractVerificationLink(sent.Body);

        var response = await client.PostAsJsonAsync(
            "/verify-email",
            new { userId, token = "not-a-valid-base64-token!!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsEmailConfirmedAsync(host.Services, email));
    }

    [RequiresDatabaseFact]
    public async Task Verify_email_rejects_an_unknown_user_id()
    {
        var host = CreateHost();
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/verify-email",
            new { userId = Guid.NewGuid().ToString(), token = "anything" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Verify_email_is_a_noop_once_already_confirmed()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractVerificationLink(sent.Body);

        var first = await client.PostAsJsonAsync("/verify-email", new { userId, token });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same link, clicked again (e.g. a double click, or a stale browser tab).
        var second = await client.PostAsJsonAsync("/verify-email", new { userId, token });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.True(await IsEmailConfirmedAsync(host.Services, email));
    }

    [RequiresDatabaseFact]
    public async Task Resend_verification_returns_ok_but_sends_nothing_for_an_unknown_email()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"nobody-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/resend-verification", new { email });

        // Never distinguishable from a hit: this is the whole point of the
        // endpoint always answering 200 (see IEmailVerificationService).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(emails.Sent, m => m.To == email);
    }

    [RequiresDatabaseFact]
    public async Task Resend_verification_sends_an_email_for_a_registered_but_unconfirmed_user()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        // Created directly through UserManager, bypassing /register, so this
        // user has never had a verification email sent - no cooldown is
        // active for them yet.
        await CreateUnconfirmedUserAsync(host.Services, email);

        var response = await client.PostAsJsonAsync("/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(emails.Sent, m => m.To == email);
    }

    [RequiresDatabaseFact]
    public async Task Resend_verification_is_throttled_immediately_after_registering()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        // Registration itself sends the first email and starts the cooldown.
        await RegisterAsync(client, email);
        Assert.Single(emails.Sent, m => m.To == email);

        var response = await client.PostAsJsonAsync("/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Still just the one email from registration - the resend was
        // silently throttled, not queued a second time.
        Assert.Single(emails.Sent, m => m.To == email);
    }

    [RequiresDatabaseFact]
    public async Task Resend_verification_returns_ok_but_sends_nothing_for_an_already_confirmed_user()
    {
        var host = CreateHost();
        var client = host.CreateClient();
        var emails = (RecordingEmailSender)host.Services.GetRequiredService<IEmailSender>();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        var sent = Assert.Single(emails.Sent, m => m.To == email);
        var (userId, token) = ExtractVerificationLink(sent.Body);
        await client.PostAsJsonAsync("/verify-email", new { userId, token });

        var response = await client.PostAsJsonAsync("/resend-verification", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Still just the one email from registration.
        Assert.Single(emails.Sent, m => m.To == email);
    }
}
