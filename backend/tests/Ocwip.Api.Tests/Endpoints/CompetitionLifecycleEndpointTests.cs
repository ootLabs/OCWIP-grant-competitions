using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The operator's way through a competition, over real HTTP (T-20).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionLifecycleEndpointTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionLifecycleEndpointTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseFact]
    public async Task A_new_competition_is_a_draft_whatever_the_caller_asked_for()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // Act
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Assert
        // The request carries no status field at all, which is the point: the
        // only way into Published is the transition, and T-22 puts a confirmed
        // click in front of it.
        Assert.Equal(CompetitionStatus.Draft, competition.Status);
        Assert.Null(competition.PublishedAt);
        Assert.True(competition.IsActive);

        // The panel draws its buttons from the rule rather than from a copy.
        Assert.Equal([CompetitionStatus.Published], competition.AllowedTransitions);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_walks_the_whole_lifecycle_and_the_clock_walks_its_part()
    {
        // Arrange
        var (host, clock) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act, Assert
        var published = await Move(client, competition.Id, CompetitionStatus.Published);
        Assert.Equal(CompetitionStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAt);

        // Nobody presses anything here. The intake opens because the start
        // date arrived, which is the report's "przejścia dzieją się same".
        //
        // Signed in again after each jump, and that is not test scaffolding:
        // the cookie handler reads the same clock, so a month of it passing
        // expires the session exactly as it would in the product. A test that
        // reused the old cookie would be asserting against a session the
        // application had already ended.
        clock.Now = CompetitionTestHost.Start;
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var open = await Read(client, competition.Id);
        Assert.Equal(CompetitionStatus.OpenForApplications, open.Status);

        clock.Now = CompetitionTestHost.End;
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var closed = await Read(client, competition.Id);
        Assert.Equal(CompetitionStatus.Closed, closed.Status);

        // And from the closed state a person takes over again.
        var underReview = await Move(
            client, competition.Id, CompetitionStatus.UnderReview);
        Assert.Equal(CompetitionStatus.UnderReview, underReview.Status);

        var resolved = await Move(client, competition.Id, CompetitionStatus.Resolved);
        Assert.Equal(CompetitionStatus.Resolved, resolved.Status);

        var archived = await Move(client, competition.Id, CompetitionStatus.Archived);
        Assert.Equal(CompetitionStatus.Archived, archived.Status);
        Assert.Empty(archived.AllowedTransitions);
    }

    [RequiresDatabaseFact]
    public async Task A_move_the_table_does_not_allow_is_refused_and_named()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        // Skipping the intake entirely: a draft cannot be resolved.
        var response = await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Resolved);

        var body = await response.Content.ReadAsStringAsync();

        // Assert
        // 409 and not 400: the request is well formed, the competition is
        // simply not somewhere that move can be made from.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // D12: the message names where the competition actually is, so the
        // operator does not have to work out which rule they tripped over.
        Assert.Contains("roboczy", body);
        Assert.Contains("rozstrzygnięty", body);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_cannot_open_an_intake_before_its_start_date()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var competition = await CompetitionTestHost.CreateAsync(client);
        await Move(client, competition.Id, CompetitionStatus.Published);

        // Act
        var response = await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.OpenForApplications);

        // Assert
        // The pair is in the transition table, but only as a scheduled one.
        // The dates decide when an intake opens, and this is the test that
        // notices if that ever stops being true.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_never_closes_by_itself_and_can_be_closed_by_hand()
    {
        // Arrange
        var (host, clock) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var competition = await CompetitionTestHost.CreateAsync(
            client, CompetitionTestHost.Request(continuous: true));

        await Move(client, competition.Id, CompetitionStatus.Published);

        // Act
        clock.Now = CompetitionTestHost.Start.AddYears(5);

        // A new session at the new moment: the cookie handler reads this same
        // clock, so the old one is five years stale.
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var open = await Read(client, competition.Id);

        var closed = await Move(client, competition.Id, CompetitionStatus.Closed);

        // Assert
        // Five years past the start and still taking applications, because
        // there is no closing date. An empty date read as a date in the past
        // would have closed this competition before it opened.
        Assert.Equal(CompetitionStatus.OpenForApplications, open.Status);
        Assert.Null(open.EndDate);
        Assert.True(open.IsContinuousIntake);

        // And the operator row in the table is what lets it be closed at all.
        Assert.Equal(CompetitionStatus.Closed, closed.Status);
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_competition_stays_in_the_database_and_refuses_changes()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        var deleted = await client.DeleteAsync($"/competitions/{competition.Id}");
        var body = await deleted.Content.ReadFromJsonAsync<CompetitionResponse>();

        var again = await client.DeleteAsync($"/competitions/{competition.Id}");
        var read = await client.GetAsync($"/competitions/{competition.Id}");
        var move = await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        // Assert
        // AGENTS.md, security rule 5: nothing is deleted, so the row is still
        // there and an operator still reads it.
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.False(body!.IsActive);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Idempotent, for the same reason as logging out: asking for the state
        // something is already in is not a failure.
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        // But it is not worked on any more.
        Assert.Equal(HttpStatusCode.Conflict, move.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Two_competitions_cannot_share_a_number()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request();
        await CompetitionTestHost.CreateAsync(client, request);

        // Act
        var response = await client.PostAsJsonAsync(
            "/competitions", request with { Title = "Inny konkurs" });

        // Assert
        // Answered as a message rather than as the 500 the unique index would
        // produce on its own.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("numer", await response.Content.ReadAsStringAsync());
    }

    private static async Task<CompetitionResponse> Move(
        HttpClient client,
        Guid id,
        CompetitionStatus target)
    {
        var response = await CompetitionTestHost.ChangeStatusAsync(client, id, target);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CompetitionResponse>())!;
    }

    private static async Task<CompetitionResponse> Read(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/competitions/{id}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CompetitionResponse>())!;
    }
}
