using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Xunit;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// The access rules of T-13.2, exercised over real HTTP through the real
/// pipeline, on the probe routes in PolicyProbeEndpoints.
///
/// The assertions are about what a CALLER observes, for the reason the card
/// gives: a rule that is only true inside a handler is a rule that stops
/// being true the moment somebody maps an endpoint without it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthorizationLayerTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public AuthorizationLayerTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> Host() =>
        SessionTestHost.Create(
            _factory,
            _database,
            settings: new Dictionary<string, string?>
            {
                // The probes sign in repeatedly; the IP limit from T-12.5 is
                // not what any of these tests is about.
                ["RateLimiting:PermitLimit"] = "200",
            },
            services: services => services.AddPolicyProbes());

    /// <summary>
    /// Signs in an account of the given role, optionally attached to a real
    /// Podmiot. Real, because users.entity_id carries a foreign key: a made up
    /// identifier is refused by the schema, which is the schema doing its job.
    /// </summary>
    private async Task<(HttpClient Client, Guid? EntityId)> SignedInAs(
        WebApplicationFactory<Program> host,
        Role role,
        bool withEntity = false)
    {
        var email = SessionTestHost.Email(role.ToString().ToLowerInvariant());
        var user = await SessionTestHost.CreateAccountAsync(host, email, role);

        Guid? entityId = null;

        if (withEntity)
        {
            await using var context = _database.CreateContext();

            var entity = TestEntity.New();
            context.Entities.Add(entity);
            await context.SaveChangesAsync();

            var stored = await context.Users.SingleAsync(x => x.Id == user.Id);
            stored.EntityId = entity.Id;
            await context.SaveChangesAsync();

            entityId = entity.Id;
        }

        var client = host.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/login", new { email, password = SessionTestHost.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return (client, entityId);
    }

    [RequiresDatabaseFact]
    public async Task An_endpoint_that_declares_no_rule_is_refused_not_served()
    {
        // The card's overriding principle and the reason it is a fallback
        // policy rather than a habit: this route asks for nothing at all, and
        // the framework still refuses it. An endpoint somebody forgets is the
        // one this has to cover, because a forgotten rule looks exactly like a
        // deliberately public endpoint in review.
        var client = SessionTestHost.RawClient(Host());

        var response = await client.GetAsync(PolicyProbeEndpoints.NoRuleAtAll);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_anonymous_caller_gets_401_and_a_signed_in_one_without_the_role_gets_403()
    {
        // Two different answers to two different questions, and the card asks
        // for the second one by name. 401 means "say who you are", 403 means
        // "you said, and it is not enough": a signed in applicant retrying
        // with the same cookie learns nothing new, so telling the browser to
        // ask for credentials again would be a loop.
        var host = Host();
        var anonymous = SessionTestHost.RawClient(host);
        var (applicant, _) = await SignedInAs(host, Role.Applicant);

        var withoutSession = await anonymous.GetAsync(PolicyProbeEndpoints.OperatorOnly);
        var wrongRole = await applicant.GetAsync(PolicyProbeEndpoints.OperatorOnly);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_refusal_is_a_problem_document_not_a_500_and_not_an_empty_body()
    {
        // The card is explicit: a refusal is a normal state of the
        // application, not a failure. A 500 fills the log with noise that
        // real errors then drown in, and an empty page looks broken to
        // somebody who will telephone OCWIP about it.
        var host = Host();
        var (applicant, _) = await SignedInAs(host, Role.Applicant);

        var response = await applicant.GetAsync(PolicyProbeEndpoints.OperatorOnly);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body);
        // In Polish, and without naming anything internal.
        Assert.Contains("Nie masz dostępu", body);
        Assert.DoesNotContain("Exception", body);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_reaches_the_operator_route()
    {
        var host = Host();
        var (operatorClient, _) = await SignedInAs(host, Role.Operator);

        var response = await operatorClient.GetAsync(PolicyProbeEndpoints.OperatorOnly);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_sees_a_resource_belonging_to_somebody_else()
    {
        // "Operator widzi wszystko", said plainly by the client. No ownership
        // question is even asked, which is why the operator arm comes first in
        // the handler.
        var host = Host();
        var (operatorClient, _) = await SignedInAs(host, Role.Operator);

        var response = await operatorClient.GetAsync(
            $"{PolicyProbeEndpoints.OwnedResource}?entityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_sees_their_own_entity_resource_and_not_another_one()
    {
        // The rule the whole card exists for, and the one the client stated:
        // an applicant sees their own piece and nothing else. Two applicants
        // hold the same role here, which is exactly why a role attribute
        // could not have expressed this.
        var host = Host();
        var (applicant, mine) = await SignedInAs(
            host, Role.Applicant, withEntity: true);

        var own = await applicant.GetAsync(
            $"{PolicyProbeEndpoints.OwnedResource}?entityId={mine}");
        var other = await applicant.GetAsync(
            $"{PolicyProbeEndpoints.OwnedResource}?entityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_applicant_without_an_entity_owns_nothing()
    {
        // Every account looks like this today, because registration does not
        // create a Podmiot yet (B-09), so this is the common case rather than
        // an edge one. Without the explicit guard in ResourceOwnership the
        // comparison would read .Value off a null and answer 500 instead of
        // refusing.
        var host = Host();
        var (applicant, _) = await SignedInAs(host, Role.Applicant);

        var response = await applicant.GetAsync(
            $"{PolicyProbeEndpoints.OwnedResource}?entityId={Guid.Empty}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_unknown_path_is_a_404_and_not_a_demand_to_log_in()
    {
        // The fallback policy reaches requests that matched no endpoint at
        // all, so without the terminal route in Program.cs a mistyped path
        // answers 401. Asserted from both sides on purpose: anonymously
        // because a 401 hints that an invented path might exist behind a
        // login, and signed in because the panels will read 401 as "the
        // session died" and would sign the user out over a typo.
        var host = Host();
        var anonymous = SessionTestHost.RawClient(host);
        var (applicant, _) = await SignedInAs(host, Role.Applicant);

        var withoutSession = await anonymous.GetAsync("/no-such-path");
        var withSession = await applicant.GetAsync("/no-such-path");

        Assert.Equal(HttpStatusCode.NotFound, withoutSession.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withSession.StatusCode);
        Assert.Equal(
            "application/problem+json",
            withoutSession.Content.Headers.ContentType?.MediaType);
    }

    [RequiresDatabaseFact]
    public async Task A_reviewer_is_refused_a_resource_that_is_not_an_application()
    {
        // Refused deliberately, not by omission. A reviewer may see the
        // applications an operator assigned to them (T-37), and the
        // assignment table Authorization/EntityScopedHandler.cs reads only
        // ever grants access to an Application; the probe resource here is
        // neither an application nor anything an assignment could name, so
        // the honest answer stays no. The positive case, a real application
        // an assignment DOES cover, is ReviewerAssignmentTests, which goes
        // through the product's own endpoints rather than this probe.
        var host = Host();
        var (reviewer, _) = await SignedInAs(host, Role.Reviewer);

        var response = await reviewer.GetAsync(
            $"{PolicyProbeEndpoints.OwnedResource}?entityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
