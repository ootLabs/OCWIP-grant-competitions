using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-111: behind the reverse proxy. The login limiter tells two clients of
/// the trusted proxy apart and ignores the forwarded address of a sender it
/// does not trust; an unhandled failure answers as ProblemDetails.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReverseProxyTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private const string Proxy = "10.0.0.5";
    private const string PeerHeader = "X-Test-Peer";

    /// <summary>
    /// The test server has no network, so the connection's address comes from
    /// a header, set before the application's own pipeline runs.
    /// </summary>
    private sealed class PeerFromHeader : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, pipeline) =>
            {
                if (context.Request.Headers.TryGetValue(PeerHeader, out var peer))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(peer.ToString());
                }

                return pipeline(context);
            });
            next(app);
        };
    }

    private WebApplicationFactory<Program> Host() =>
        SessionTestHost.Create(factory, database,
            settings: new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["ForwardedHeaders:KnownProxies"] = Proxy,
            },
            services: s => s.AddSingleton<IStartupFilter, PeerFromHeader>());

    private static async Task<HttpStatusCode> SignInAsync(HttpClient client, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/login")
        {
            Content = JsonContent.Create(new { email = "nikt@example.org", password = "zle-haslo" }),
        };
        request.Headers.Add(PeerHeader, peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [RequiresDatabaseFact]
    public async Task Two_clients_of_the_proxy_have_their_own_limit()
    {
        var client = Host().CreateClient();

        await SignInAsync(client, Proxy, "198.51.100.1");
        await SignInAsync(client, Proxy, "198.51.100.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, await SignInAsync(client, Proxy, "198.51.100.1"));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, await SignInAsync(client, Proxy, "198.51.100.2"));
    }

    [RequiresDatabaseFact]
    public async Task A_sender_that_is_not_the_proxy_cannot_choose_its_address()
    {
        var client = Host().CreateClient();

        // A new forwarded address on every try, from a machine that is not the proxy.
        await SignInAsync(client, "203.0.113.9", "198.51.100.11");
        await SignInAsync(client, "203.0.113.9", "198.51.100.12");

        Assert.Equal(HttpStatusCode.TooManyRequests, await SignInAsync(client, "203.0.113.9", "198.51.100.13"));
    }

    private sealed class FailingAccounts : IAccountService
    {
        public Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Sekretny szczegół awarii.");
    }

    [RequiresDatabaseFact]
    public async Task An_unhandled_failure_answers_as_problem_details_without_its_details()
    {
        var client = SessionTestHost.Create(factory, database,
            settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "200" },
            services: s => s.AddScoped<IAccountService, FailingAccounts>()).CreateClient();

        var response = await client.PostAsJsonAsync("/register",
            new RegisterRequest(SessionTestHost.Email("awaria"), SessionTestHost.Password, "Ada", "Testowa", AcceptedConsents: TestConsents.All));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Sekretny", await response.Content.ReadAsStringAsync());

        // T-112: the exception handler clears the headers of a failed answer;
        // the security headers are set after it, when the answer starts.
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }
}
