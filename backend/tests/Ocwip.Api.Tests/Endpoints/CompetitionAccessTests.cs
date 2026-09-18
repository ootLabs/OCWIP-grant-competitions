using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Who may touch a competition (T-20, on the layer from T-13.2).
///
/// The card asks for one negative test, an applicant and a reviewer refused
/// when they try to publish. It is written here as a sweep over every operator
/// route instead, because a rule proven on one route is a rule that stops
/// being true the moment somebody maps the next one.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionAccessTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionAccessTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseTheory]
    [InlineData(Role.Applicant)]
    [InlineData(Role.Reviewer)]
    public async Task Publishing_a_competition_is_refused_to_everyone_but_an_operator(
        Role role)
    {
        // Arrange
        var (host, _) = Host();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        var client = await CompetitionTestHost.SignedInAs(host, role);

        // Act
        var response = await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        // Assert
        // 403 and not 401: the session is perfectly good, and signing in again
        // would change nothing.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseTheory]
    [InlineData(Role.Applicant)]
    [InlineData(Role.Reviewer)]
    public async Task Every_operator_route_is_refused_to_the_other_roles(Role role)
    {
        // Arrange
        var (host, _) = Host();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        var client = await CompetitionTestHost.SignedInAs(host, role);

        // Act
        var responses = new[]
        {
            await client.PostAsJsonAsync(
                "/competitions", CompetitionTestHost.Request()),
            await client.GetAsync("/competitions"),
            await client.GetAsync($"/competitions/{competition.Id}"),
            await client.PutAsJsonAsync(
                $"/competitions/{competition.Id}", CompetitionTestHost.Request()),
            await CompetitionTestHost.ChangeStatusAsync(
                client, competition.Id, CompetitionStatus.Published),
            await client.DeleteAsync($"/competitions/{competition.Id}"),
        };

        // Assert
        Assert.All(responses, response =>
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [RequiresDatabaseFact]
    public async Task An_anonymous_caller_is_asked_to_sign_in_for_the_operator_routes()
    {
        // Arrange
        var (host, _) = Host();
        var client = host.CreateClient();

        // Act
        var list = await client.GetAsync("/competitions");
        var create = await client.PostAsJsonAsync(
            "/competitions", CompetitionTestHost.Request());

        // Assert
        // 401 rather than 403 here, because there is no session to judge yet.
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_public_routes_answer_without_an_account()
    {
        // Arrange
        var (host, _) = Host();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(operatorClient);

        await CompetitionTestHost.ChangeStatusAsync(
            operatorClient, competition.Id, CompetitionStatus.Published);

        var guest = host.CreateClient();

        // Act
        var list = await guest.GetAsync("/public/competitions");
        var single = await guest.GetAsync($"/public/competitions/{competition.Id}");

        // Assert
        // D6: an applicant should not have to create an account to find out
        // what OCWIP is running. The fallback policy from T-13.2 refuses
        // anything that does not say it is public, so this is a guard against
        // the two AllowAnonymous calls quietly disappearing.
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }
}
