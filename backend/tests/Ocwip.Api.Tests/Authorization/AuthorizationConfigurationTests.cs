using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Authorization;
using Ocwip.Api.Configuration;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// The shape of the rule set itself (T-13.2), read back the way the
/// application reads it.
///
/// Separate from AuthorizationLayerTests, which asks what a caller observes.
/// This class asks what was CONFIGURED, because two of the card's criteria are
/// about the configuration rather than about any one request: that a missing
/// rule denies, and that a new role is a value in an enum rather than a
/// rewrite.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthorizationConfigurationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public AuthorizationConfigurationTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private static IAuthorizationPolicyProvider Policies()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddOcwipAuthorization(hasStore: false);

        return services
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationPolicyProvider>();
    }

    [Fact]
    public async Task The_fallback_policy_demands_a_signed_in_caller()
    {
        // The criterion spelled out as configuration: an endpoint with no
        // authorization metadata is evaluated against THIS policy, so
        // forgetting a rule denies instead of publishing.
        var fallback = await Policies().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(
            fallback.Requirements,
            requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Every_role_in_the_enum_has_a_policy()
    {
        // R-02 proposes a fourth role. The instruction in rozbieznosci.md is
        // to build the policies so that adding one is a value in the enum,
        // not a rewrite, and this walks the enum to prove the loop in
        // AddOcwipAuthorization really does that: add Administrator tomorrow
        // and its policy exists without anyone touching this file either.
        var provider = Policies();

        foreach (var role in Enum.GetValues<Role>())
        {
            var policy = await provider.GetPolicyAsync(
                AuthorizationConfiguration.Names.For(role));

            Assert.NotNull(policy);
            Assert.Contains(
                policy.Requirements,
                requirement => requirement is RolesAuthorizationRequirement roles
                    && roles.AllowedRoles.Contains(role.ToString()));
        }
    }

    [Fact]
    public async Task The_resource_policy_carries_the_entity_scoped_requirement()
    {
        var policy = await Policies().GetPolicyAsync(
            AuthorizationConfiguration.Names.OwnsResource);

        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is EntityScopedRequirement);
    }

    [RequiresDatabaseTheory]
    // The regression guard for the fallback policy, which is the risky half of
    // this card: every one of these is reachable without a session today, and
    // a default deny that silently swallowed one of them would break
    // registration, sign in, the mail links or the probes the container and
    // scripts/smoke_test.py depend on.
    [InlineData("GET", "/health")]
    [InlineData("GET", "/health/db")]
    [InlineData("POST", "/register")]
    [InlineData("POST", "/login")]
    [InlineData("POST", "/logout")]
    [InlineData("POST", "/verify-email")]
    [InlineData("POST", "/resend-verification")]
    [InlineData("POST", "/forgot-password")]
    [InlineData("POST", "/reset-password")]
    public async Task A_public_route_stays_reachable_without_a_session(
        string method, string path)
    {
        // Arrange
        var client = SessionTestHost.RawClient(
            SessionTestHost.Create(_factory, _database));

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        // Act
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        // Not "succeeds", because an empty body is a bad request to most of
        // these, and /login answers its own 401 for empty credentials. The
        // claim is narrower and sharper: whatever refused, it was not the
        // authorization layer, and the way to tell is that the pipeline
        // refuses in words of its own (AuthenticationConfiguration).
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("żeby zobaczyć tę stronę", body);
        Assert.DoesNotContain("Nie masz dostępu do tej strony", body);
    }

    [RequiresDatabaseFact]
    public async Task The_openapi_document_stays_reachable_for_the_type_generator()
    {
        // npm run api:generate reads this with no session, so the fallback
        // policy would otherwise break the frontend build. What keeps the
        // document from being a public map of an API over personal data is
        // that it exists in Development only, decided in T-17.
        var client = SessionTestHost.RawClient(
            SessionTestHost.Create(_factory, _database));

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
