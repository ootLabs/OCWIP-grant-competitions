using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The intake cut off over HTTP (T-21).
///
/// The rule itself is pinned in CompetitionIntakeTests. What these tests are
/// about is that it reaches the caller: both the guest page and the operator
/// screen are told whether applications are being taken, by the rule, so that
/// nothing on the other side has to compare two dates of its own. The card
/// says the front shows what the rule returns, and this is where that stops
/// being a sentence in a document.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionIntakeEndpointTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionIntakeEndpointTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseFact]
    public async Task The_public_page_is_told_when_the_intake_opens_and_closes()
    {
        // Arrange
        var (host, clock) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(client);

        await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        var guest = host.CreateClient();

        // Act
        var announced = await Public(guest, competition.Id);

        clock.Now = CompetitionTestHost.End.AddMinutes(-1);
        var lastMinute = await Public(guest, competition.Id);

        clock.Now = CompetitionTestHost.End;
        var closed = await Public(guest, competition.Id);

        // Assert
        // Announced and not yet fileable, open a minute before the deadline,
        // shut in the closing minute itself. D7 seen from the outside.
        Assert.Equal(IntakeState.NotYetOpen, announced.Intake.State);
        Assert.False(announced.Intake.AcceptsApplications);

        Assert.True(lastMinute.Intake.AcceptsApplications);

        Assert.False(closed.Intake.AcceptsApplications);
        Assert.Equal(IntakeState.Closed, closed.Intake.State);

        // The countdown T-23 draws needs the moment, not a rendered string.
        Assert.Equal(CompetitionTestHost.End, closed.Intake.ClosesAt);

        // D12: the refusal carries the deadline it is refusing against, in the
        // hour the applicant reads off their own wall.
        Assert.Contains("30.09.2026 o godzinie 14:00", closed.Intake.Message);
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_never_reports_itself_as_closed()
    {
        // Arrange
        var (host, clock) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var competition = await CompetitionTestHost.CreateAsync(
            client, CompetitionTestHost.Request(continuous: true));

        await CompetitionTestHost.ChangeStatusAsync(
            client, competition.Id, CompetitionStatus.Published);

        clock.Now = CompetitionTestHost.End.AddYears(5);

        // Act
        var body = await Public(host.CreateClient(), competition.Id);

        // Assert
        // Five years past the date the competition does not have, and still
        // open. An empty closing column is not a deadline in the past.
        Assert.True(body.Intake.AcceptsApplications);
        Assert.Null(body.Intake.ClosesAt);
    }

    [RequiresDatabaseFact]
    public async Task The_operator_sees_the_same_answer_as_the_applicant()
    {
        // Arrange
        var (host, clock) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var created = await CompetitionTestHost.CreateAsync(client);

        await CompetitionTestHost.ChangeStatusAsync(
            client, created.Id, CompetitionStatus.Published);

        // A new session at the new moment: the cookie handler reads this same
        // clock, so the old cookie is a month stale.
        clock.Now = CompetitionTestHost.Start;
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // Act
        var operatorView = (await (await client.GetAsync(
            $"/competitions/{created.Id}"))
            .Content.ReadFromJsonAsync<CompetitionResponse>())!;

        var guestView = await Public(host.CreateClient(), created.Id);

        // Assert
        // One rule, two readers. An operator screen that answered differently
        // would be a second copy of the cut off, which is the thing this card
        // exists to prevent.
        Assert.True(operatorView.Intake.AcceptsApplications);
        Assert.Equal(guestView.Intake, operatorView.Intake);
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_competition_takes_nothing_however_open_its_window_is()
    {
        // Arrange
        var (host, clock) = Host();

        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var created = await CompetitionTestHost.CreateAsync(client);

        await CompetitionTestHost.ChangeStatusAsync(
            client, created.Id, CompetitionStatus.Published);

        clock.Now = CompetitionTestHost.Start;
        client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        await client.DeleteAsync($"/competitions/{created.Id}");

        // Act
        var body = (await (await client.GetAsync(
            $"/competitions/{created.Id}"))
            .Content.ReadFromJsonAsync<CompetitionResponse>())!;

        // Assert
        // Inside its own window by the calendar. The row is kept for the
        // retention period, not to carry on collecting applications.
        Assert.False(body.Intake.AcceptsApplications);
        Assert.Equal(IntakeState.Unavailable, body.Intake.State);
    }

    private static async Task<PublicCompetitionResponse> Public(
        HttpClient guest,
        Guid id)
    {
        var response = await guest.GetAsync($"/public/competitions/{id}");

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<PublicCompetitionResponse>())!;
    }
}
