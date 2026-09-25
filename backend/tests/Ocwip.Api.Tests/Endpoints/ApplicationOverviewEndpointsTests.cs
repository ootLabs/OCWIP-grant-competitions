using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// "Moje wnioski", over real HTTP and a real PostgreSQL (T-34): the caller's
/// own drafts and submitted applications across every competition, and
/// nothing that belongs to anybody else.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationOverviewEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ApplicationOverviewEndpointsTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task Lists_the_callers_own_drafts_and_submitted_applications_across_competitions()
    {
        // Arrange: two competitions, one draft in the first and one
        // submitted application in the second, both under the same Podmiot.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var first = await PublishedCompetitionWithFormAsync(host);
        var second = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        var draft = await CreateAsync(applicant, first.Id);

        var toSubmit = await CreateAsync(applicant, second.Id);
        await SaveAsync(
            applicant, toSubmit.Id, FormDefinitionSamples.Parse("""{"opis":"Nasz projekt"}"""));
        var submitResponse = await applicant.PostAsJsonAsync(
            $"/applications/{toSubmit.Id}/submit", new { });
        submitResponse.EnsureSuccessStatusCode();
        var submitted = (await submitResponse.Content
            .ReadFromJsonAsync<ApplicationResponse>())!;

        // Act
        var overview = await ListMineAsync(applicant);

        // Assert: both rows are there, each naming its own competition, and
        // the submitted one carries the number the draft never had.
        Assert.Equal(2, overview.Count);

        var draftRow = Assert.Single(overview, x => x.Id == draft.Id);
        Assert.Equal(first.Id, draftRow.CompetitionId);
        Assert.Equal(first.Number, draftRow.CompetitionNumber);
        Assert.Equal(ApplicationStatus.Draft, draftRow.Status);
        Assert.Null(draftRow.Number);
        Assert.Null(draftRow.SubmittedAt);

        var submittedRow = Assert.Single(overview, x => x.Id == submitted.Id);
        Assert.Equal(second.Id, submittedRow.CompetitionId);
        Assert.Equal(ApplicationStatus.Submitted, submittedRow.Status);
        Assert.Equal(submitted.Number, submittedRow.Number);
        Assert.Equal(submitted.SubmittedAt, submittedRow.SubmittedAt);
    }

    [RequiresDatabaseFact]
    public async Task Several_offers_of_the_same_entity_in_one_competition_stay_distinct_rows()
    {
        // Arrange: D9, one Podmiot, two applications, same competition.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);

        var one = await CreateAsync(applicant, competition.Id);
        var two = await CreateAsync(applicant, competition.Id);

        // Act
        var overview = await ListMineAsync(applicant);

        // Assert
        Assert.Equal(2, overview.Count);
        Assert.Contains(overview, x => x.Id == one.Id);
        Assert.Contains(overview, x => x.Id == two.Id);
    }

    [RequiresDatabaseFact]
    public async Task A_deactivated_draft_disappears_from_the_list_as_T29_promises()
    {
        // Arrange
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host);
        var draft = await CreateAsync(applicant, competition.Id);

        var deactivateResponse = await applicant.DeleteAsync($"/applications/{draft.Id}");
        deactivateResponse.EnsureSuccessStatusCode();

        // Act
        var overview = await ListMineAsync(applicant);

        // Assert: gone from the list, per T-29's own promise, even though
        // GET /applications/{id} still reads it back.
        Assert.Empty(overview);
    }

    [RequiresDatabaseFact]
    public async Task Never_lists_another_applicants_applications()
    {
        // Arrange: two Podmiot, one draft each. Rule 2 of AGENTS.md.
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);

        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (owner, _, _) = await SeedApplicantAsync(host);
        var (stranger, _, _) = await SeedApplicantAsync(host);

        await CreateAsync(owner, competition.Id);

        // Act
        var strangerOverview = await ListMineAsync(stranger);

        // Assert
        Assert.Empty(strangerOverview);
    }

    [RequiresDatabaseFact]
    public async Task An_account_without_a_Podmiot_is_refused_rather_than_shown_an_empty_list()
    {
        // Arrange: B-09, an account with no Podmiot at all.
        var (host, _) = CompetitionTestHost.Create(_factory, _database);
        var applicant = await CompetitionTestHost.SignedInAs(host, Role.Applicant);

        // Act
        var response = await applicant.GetAsync("/applications");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Only_an_applicant_may_read_it()
    {
        // Arrange
        var (host, _) = CompetitionTestHost.Create(_factory, _database);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var reviewerClient = await CompetitionTestHost.SignedInAs(host, Role.Reviewer);
        var anonymous = host.CreateClient();

        // Act
        var operatorResponse = await operatorClient.GetAsync("/applications");
        var reviewerResponse = await reviewerClient.GetAsync("/applications");
        var anonymousResponse = await anonymous.GetAsync("/applications");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviewerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    /// <summary>
    /// ApplicationTestHost.SeedApplicantAsync against this class's database.
    /// </summary>
    private Task<(HttpClient Client, Guid EntityId, string Email)> SeedApplicantAsync(
        WebApplicationFactory<Program> host) =>
        ApplicationTestHost.SeedApplicantAsync(host, _database);
}
