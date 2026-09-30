using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-123: a JSON body the API cannot read is the caller's mistake, so it
/// answers 400, not 500. Monitoring (T-116) counts every 5xx as an outage, and
/// one broken client must not look like one.
///
/// /login is the route under test because it needs no session. The body
/// carries a marker standing in for a password: it must reach neither the
/// answer nor the log (AGENTS.md, security rule 4).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UnreadableBodyTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private const string Marker = "SEKRETNE-HASLO-T123";

    private static readonly byte[] InvalidUtf8 =
        [.. Encoding.UTF8.GetBytes("{\"email\":\"ada@example.org\",\"password\":\""), 0xC3, 0x28, .. Encoding.UTF8.GetBytes($"{Marker}\"}}")];

    public static TheoryData<string, byte[]> Unreadable => new()
    {
        { "truncated", Encoding.UTF8.GetBytes($"{{\"email\": \"ada@example.org\", \"password\": \"{Marker}") },
        { "wrong type", Encoding.UTF8.GetBytes($"{{\"email\": 5, \"password\": \"{Marker}\"}}") },
        { "not utf-8", InvalidUtf8 },
    };

    private static async Task<HttpResponseMessage> PostBodyAsync(HttpClient client, byte[] body)
    {
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await client.PostAsync("/login", content);
    }

    private WebApplicationFactory<Program> Host(CapturedLogs logs, Action<IServiceCollection>? services = null) =>
        SessionTestHost.Create(factory, database,
            settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "200" },
            services: services).WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.AddProvider(logs)));

    [RequiresDatabaseTheory]
    [MemberData(nameof(Unreadable))]
    public async Task An_unreadable_body_answers_400_as_problem_details(string label, byte[] body)
    {
        var client = Host(new CapturedLogs()).CreateClient();

        var response = await PostBodyAsync(client, body);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{label}: {response.StatusCode}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("detail").GetString()));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
    }

    [RequiresDatabaseTheory]
    [MemberData(nameof(Unreadable))]
    public async Task An_unreadable_body_reaches_neither_the_answer_nor_the_log(string label, byte[] body)
    {
        var logs = new CapturedLogs();
        var client = Host(logs).CreateClient();

        var response = await PostBodyAsync(client, body);

        var answer = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Marker, answer);
        Assert.DoesNotContain("ada@example.org", answer);
        Assert.False(logs.Text.Contains(Marker, StringComparison.Ordinal), $"{label}: the body reached the log");
        Assert.DoesNotContain("ada@example.org", logs.Text);
    }

    [RequiresDatabaseFact]
    public async Task An_unreadable_body_is_not_logged_as_a_server_error()
    {
        var logs = new CapturedLogs();
        var client = Host(logs).CreateClient();

        await PostBodyAsync(client, InvalidUtf8);

        // The caller's mistake is not an Error: an alert on Error lines would
        // page someone for the same scanner the 5xx count no longer flags.
        Assert.DoesNotContain(logs.Messages, m => m.StartsWith("Error ", StringComparison.Ordinal) || m.StartsWith("Critical ", StringComparison.Ordinal));
    }

    [RequiresDatabaseFact]
    public async Task A_readable_body_still_reaches_the_endpoint()
    {
        var client = Host(new CapturedLogs()).CreateClient();

        var response = await client.PostAsJsonAsync("/login", new { email = "nikt@example.org", password = "zle-haslo" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class FailingSessions : ISessionService
    {
        public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sekretny szczegół awarii.");

        public Task LogoutAsync(System.Security.Claims.ClaimsPrincipal principal) => Task.CompletedTask;

        public Task<CurrentUserResponse?> CurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal) =>
            Task.FromResult<CurrentUserResponse?>(null);
    }

    [RequiresDatabaseFact]
    public async Task A_real_server_failure_still_answers_500()
    {
        var client = Host(new CapturedLogs(), s => s.AddScoped<ISessionService, FailingSessions>()).CreateClient();

        var response = await client.PostAsJsonAsync("/login", new { email = "ada@example.org", password = "Str0ng!Passw0rd" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Sekretny", await response.Content.ReadAsStringAsync());
    }
}
