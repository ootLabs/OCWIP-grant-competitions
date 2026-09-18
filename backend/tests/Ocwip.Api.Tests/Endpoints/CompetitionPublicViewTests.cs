using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// What a guest can see (T-20, feeding T-23).
///
/// The rule the card states is short and the consequences are not: a draft has
/// no public address, an inactive competition leaves the listing without
/// leaving the database, and an archived one leaves the listing while keeping
/// its address.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionPublicViewTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionPublicViewTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseFact]
    public async Task A_draft_has_no_public_address_at_all()
    {
        // Arrange
        var (host, _) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        var guest = host.CreateClient();

        // Act
        var guessed = await guest.GetAsync($"/public/competitions/{competition.Id}");
        var listed = await ListPublic(guest);

        // Assert
        // 404 rather than 403, and the difference is the whole rule: 403 tells
        // whoever typed the identifier in that they typed a real one.
        Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        Assert.DoesNotContain(listed, x => x.Id == competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task Publishing_gives_the_competition_its_public_address()
    {
        // Arrange
        var (host, _) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        // Act
        await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        var guest = host.CreateClient();
        var response = await guest.GetAsync($"/public/competitions/{competition.Id}");
        var body = await response.Content.ReadFromJsonAsync<PublicCompetitionResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Visible, but not yet taking applications: two different moments, and
        // T-23 draws two different buttons from them.
        Assert.Equal(CompetitionStatus.Published, body!.Status);

        Assert.Contains(await ListPublic(guest), x => x.Id == competition.Id);
    }

    [RequiresDatabaseFact]
    public async Task An_inactive_competition_leaves_the_public_view_and_stays_in_the_database()
    {
        // Arrange
        var (host, _) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        // Act
        await client.DeleteAsync($"/competitions/{competition.Id}");

        var guest = host.CreateClient();
        var single = await guest.GetAsync($"/public/competitions/{competition.Id}");
        var listed = await ListPublic(guest);

        var operatorView = await client.GetAsync($"/competitions/{competition.Id}");

        // Assert
        // Exactly the card's wording: it disappears from the public view only.
        Assert.Equal(HttpStatusCode.NotFound, single.StatusCode);
        Assert.DoesNotContain(listed, x => x.Id == competition.Id);
        Assert.Equal(HttpStatusCode.OK, operatorView.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task An_archived_competition_leaves_the_listing_but_keeps_its_address()
    {
        // Arrange
        var (host, clock) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        clock.Now = CompetitionTestHost.End;
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        foreach (var target in new[]
        {
            CompetitionStatus.UnderReview,
            CompetitionStatus.Resolved,
            CompetitionStatus.Archived,
        })
        {
            var moved = await CompetitionTestHost.ChangeStatusAsync(
                client, competition.Id, target);

            moved.EnsureSuccessStatusCode();
        }

        var guest = host.CreateClient();

        // Act
        var single = await guest.GetAsync($"/public/competitions/{competition.Id}");
        var listed = await ListPublic(guest);

        // Assert
        // Out of the current listing, still readable at the same address. A
        // permanent link that stops working the day the competition is filed
        // away is not permanent, and the public results archive (R-14) is
        // built out of exactly these.
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
        Assert.DoesNotContain(listed, x => x.Id == competition.Id);
    }

    private static async Task<IReadOnlyList<PublicCompetitionResponse>> ListPublic(
        HttpClient client)
    {
        var response = await client.GetAsync("/public/competitions");
        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<IReadOnlyList<PublicCompetitionResponse>>())!;
    }
}
