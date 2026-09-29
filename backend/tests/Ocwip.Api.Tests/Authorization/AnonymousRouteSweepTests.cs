using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// Every route the application maps, read from its own endpoint table, not
/// from a list somebody keeps (AGENTS.md: no rule means no access).
///
/// The fallback policy from T-13.2 refuses a route that declares nothing, and
/// AuthorizationLayerTests proves that on a probe. What it cannot prove is
/// the next product route: one AllowAnonymous too many, or a handler of its
/// own that lets a caller without a session through, and nothing turns red.
/// So the public routes are a reviewed list here, and every other route is
/// asked, without a session, whether it answers.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed partial class AnonymousRouteSweepTests : IClassFixture<OcwipWebApplicationFactory>
{
    /// <summary>
    /// The routes that answer without an account, each one a decision. A new
    /// one belongs here only with a reason a reviewer can check.
    /// </summary>
    private static readonly string[] PublicRoutes =
    [
        // Unknown paths: a 404, not a demand to sign in (T-13.2).
        "* /{*path}",
        // The probes the container, Caddy and scripts/smoke_test.py call.
        "GET /health",
        "GET /health/db",
        // Development only (T-17), read by npm run api:generate without a session.
        "GET /openapi/{documentName}.json",
        // What OCWIP runs and what it funded, for somebody without an account (D6, R-14).
        "GET /public/competitions",
        "GET /public/competitions/{id:guid}",
        "GET /public/competitions/{competitionId:guid}/results",
        "GET /public/results",
        // A requirement's template to download before registering (T-102).
        "GET /public/attachment-templates/{requirementId:guid}",
        // The terms and the privacy notice, read before an account exists (T-107, T-121).
        "GET /public/consents",
        // Getting an account and getting back into it: the caller has no session yet.
        "POST /register",
        "POST /verify-email",
        "POST /resend-verification",
        "POST /login",
        "POST /logout",
        "POST /forgot-password",
        "POST /reset-password",
        // The link from the new address's mailbox, which may be opened signed out (T-106).
        "POST /confirm-email-change",
    ];

    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public AnonymousRouteSweepTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public void The_public_routes_are_exactly_the_reviewed_list()
    {
        // Arrange
        var host = SessionTestHost.Create(_factory, _database);

        // Act
        var anonymous = Routes(host)
            .Where(route => route.Anonymous)
            .Select(route => route.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        // The whole list in the message, so the failure says what changed.
        Assert.True(
            PublicRoutes.Order(StringComparer.Ordinal).SequenceEqual(anonymous),
            "Public routes now:\n" + string.Join("\n", anonymous));
    }

    [RequiresDatabaseFact]
    public async Task Every_other_route_refuses_a_caller_without_a_session()
    {
        // Arrange
        var host = SessionTestHost.Create(_factory, _database);
        var client = SessionTestHost.RawClient(host);
        var protectedRoutes = Routes(host).Where(route => !route.Anonymous).ToArray();

        // Act
        var answers = new List<string>();
        foreach (var route in protectedRoutes)
        {
            // "*" is a route mapped for every method; GET asks it as well as any.
            var method = route.Method == "*" ? HttpMethod.Get : new HttpMethod(route.Method);
            var request = new HttpRequestMessage(method, Concrete(route.Pattern));
            if (route.Multipart)
            {
                // An upload route is only matched for the type it accepts;
                // anything else never reaches the authorization layer.
                request.Content = new MultipartFormDataContent { { new ByteArrayContent([1]), "file", "x.pdf" } };
            }
            else if (route.Method is "POST" or "PUT" or "PATCH")
            {
                request.Content = JsonContent.Create(new { });
            }

            var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                answers.Add($"{route.Name} -> {(int)response.StatusCode}");
            }
        }

        // Assert
        // A 404 fails too: it means the sample values below did not fit the
        // route's constraints, so the authorization layer was never asked.
        Assert.NotEmpty(protectedRoutes);
        Assert.True(answers.Count == 0, "Answered without a session:\n" + string.Join("\n", answers));
    }

    private sealed record Route(string Method, string Pattern, bool Anonymous, bool Multipart)
    {
        public string Name => $"{Method} {Pattern}";
    }

    private static IEnumerable<Route> Routes(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => new Route(
                    method,
                    "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'),
                    endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                    endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes
                        .Any(type => type.StartsWith("multipart/", StringComparison.Ordinal)) == true)))
            .Distinct();

    /// <summary>A path that matches the pattern, constraints included, with made up values.</summary>
    private static string Concrete(string pattern) =>
        Parameter().Replace(pattern, match => match.Groups["constraint"].Value switch
        {
            "guid" => Guid.NewGuid().ToString(),
            "int" or "long" => "1",
            _ => "x",
        });

    [GeneratedRegex(@"\{\*?(?<name>[a-zA-Z]+)(:(?<constraint>[a-z]+))?[^}]*\}")]
    private static partial Regex Parameter();
}
