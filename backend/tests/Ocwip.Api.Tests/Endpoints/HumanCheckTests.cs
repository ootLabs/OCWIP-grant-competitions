using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Configuration;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The Turnstile token on the account forms: without one Cloudflare accepts,
/// the request never reaches the handler, on every route that carries it.
/// Cloudflare itself is a stand-in here, so the tests also see what the API
/// asked it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class HumanCheckTests : IClassFixture<OcwipWebApplicationFactory>
{
    private const string GoodToken = "good-token";

    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public HumanCheckTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FakeCloudflare Cloudflare) Host(
        Func<HttpResponseMessage>? answer = null)
    {
        var cloudflare = new FakeCloudflare(answer);
        var host = SessionTestHost.Create(
            _factory,
            _database,
            settings: new Dictionary<string, string?>
            {
                ["Turnstile:SecretKey"] = "test-secret",
                ["RateLimiting:PermitLimit"] = "200",
            },
            services: services => services
                .AddHttpClient(HumanCheckConfiguration.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => cloudflare));
        return (host, cloudflare);
    }

    private static HttpRequestMessage Post(string path, object body, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Add(HumanCheckConfiguration.TokenHeader, token);
        }

        return request;
    }

    public static TheoryData<string, object> Routes => new()
    {
        { "/login", new { email = "ktos@example.org", password = "Str0ng!Passw0rd" } },
        { "/register", new { email = "ktos@example.org", password = "Str0ng!Passw0rd", firstName = "A", lastName = "B" } },
        { "/forgot-password", new { email = "ktos@example.org" } },
        { "/resend-verification", new { email = "ktos@example.org" } },
    };

    [RequiresDatabaseTheory]
    [MemberData(nameof(Routes))]
    public async Task A_request_without_a_token_is_refused_before_Cloudflare_is_asked(string path, object body)
    {
        var (host, cloudflare) = Host();
        var client = host.CreateClient();

        var response = await client.SendAsync(Post(path, body, token: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(HumanCheckConfiguration.RefusedMessage, problem!.Detail);
        Assert.Equal(HumanCheckConfiguration.ProblemType, problem.Type);
        Assert.Empty(cloudflare.Requests);
    }

    [RequiresDatabaseFact]
    public async Task A_token_Cloudflare_refuses_does_not_reach_the_password_check()
    {
        var (host, cloudflare) = Host(() => Answer("""{"success":false,"error-codes":["timeout-or-duplicate"]}"""));
        var client = host.CreateClient();

        var response = await client.SendAsync(Post("/login", new { email = "ktos@example.org", password = "x" }, "used-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(cloudflare.Requests);
    }

    [RequiresDatabaseFact]
    public async Task A_good_token_lets_the_person_sign_in_and_Cloudflare_gets_the_secret_and_the_token()
    {
        var (host, cloudflare) = Host();
        var client = host.CreateClient();
        var email = SessionTestHost.Email("czlowiek");
        await SessionTestHost.CreateAccountAsync(host, email);

        var response = await client.SendAsync(
            Post("/login", new { email, password = SessionTestHost.Password }, GoodToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(cloudflare.Requests);
        Assert.Equal("test-secret", sent["secret"]);
        Assert.Equal(GoodToken, sent["response"]);
    }

    [RequiresDatabaseFact]
    public async Task Cloudflare_out_of_reach_refuses_the_request_rather_than_letting_it_through()
    {
        var (host, _) = Host(() => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = host.CreateClient();

        var response = await client.SendAsync(
            Post("/forgot-password", new { email = "ktos@example.org" }, GoodToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static HttpResponseMessage Answer(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    /// <summary>siteverify: accepts GoodToken unless told to answer otherwise, and keeps every form it got.</summary>
    private sealed class FakeCloudflare(Func<HttpResponseMessage>? answer) : HttpMessageHandler
    {
        public List<Dictionary<string, string>> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/turnstile/v0/siteverify", request.RequestUri!.AbsolutePath);
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            var fields = form.Split('&')
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
            Requests.Add(fields);

            return answer?.Invoke()
                ?? Answer(fields["response"] == GoodToken
                    ? """{"success":true,"error-codes":[]}"""
                    : """{"success":false,"error-codes":["invalid-input-response"]}""");
        }
    }
}
