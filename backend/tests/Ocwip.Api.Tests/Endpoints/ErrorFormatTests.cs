using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Ocwip.Api.Contracts;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// One error format for the whole API (T-17): failures answer with
/// application/problem+json, and the OpenAPI document says the same.
///
/// The document is the half that already regressed once. Endpoints returning
/// ProblemHttpResult have to declare their status by hand, and the obvious
/// spelling, Produces&lt;ProblemDetails&gt;, declares application/json. Runtime
/// and document then disagree, silently, and the generated TypeScript client
/// inherits the wrong one. Both halves are asserted here.
/// </summary>
public sealed class ErrorFormatTests : IClassFixture<OcwipWebApplicationFactory>
{
    private const string ProblemJson = "application/problem+json";

    private readonly OcwipWebApplicationFactory _factory;

    public ErrorFormatTests(OcwipWebApplicationFactory factory) => _factory = factory;

    /// No connection string, so registration is unavailable and answers 503.
    private HttpClient ClientWithoutDatabase() =>
        _factory
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Postgres", string.Empty))
            .CreateClient();

    /// A connection string that parses and points nowhere. Validation of the
    /// request body answers before anything opens a connection, so this covers
    /// the 400 without needing a database to be running.
    private HttpClient ClientThatNeverReachesTheDatabase() =>
        _factory
            .WithWebHostBuilder(builder => builder.UseSetting(
                "ConnectionStrings:Postgres",
                "Host=unreachable.invalid;Port=5432;Database=ocwip;Username=ocwip;Password=ocwip"))
            .CreateClient();

    [Fact]
    public async Task Registration_without_a_database_answers_in_problem_json()
    {
        var response = await ClientWithoutDatabase().PostAsJsonAsync(
            "/register",
            new RegisterRequest("adam@example.org", "Poprawne1!", "Adam", "Nowak"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_rejected_registration_answers_in_problem_json()
    {
        var response = await ClientThatNeverReachesTheDatabase().PostAsJsonAsync(
            "/register",
            new RegisterRequest("nie-jest-adresem", "x", string.Empty, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/register", "503")]
    [InlineData("/register", "400")]
    [InlineData("/verify-email", "400")]
    [InlineData("/health/db", "503")]
    public async Task The_document_declares_the_media_type_that_is_actually_sent(
        string path,
        string statusCode)
    {
        // The document is only mapped in Development (docs/architektura.md),
        // which is also the only environment that generates the client.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        var method = path == "/health/db" ? "get" : "post";
        var declared = document
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method)
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content");

        Assert.True(
            declared.TryGetProperty(ProblemJson, out _),
            $"{method.ToUpperInvariant()} {path} declares {declared} for {statusCode}, "
                + $"but the endpoint sends {ProblemJson}.");
    }
}
